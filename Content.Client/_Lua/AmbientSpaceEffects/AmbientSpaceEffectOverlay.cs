// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared.Lua.CLVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Lua.AmbientSpaceEffects;

public sealed class AmbientSpaceEffectOverlay : Overlay
{
    private const float FixedVisualRange = 8000f;
    private const int FixedMaxFields = 20;
    private readonly HashSet<EntityUid> _scratchFieldUids = new();

    private static readonly ProtoId<ShaderPrototype> FallbackShader = "AmbientNebula";
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    private static readonly StencilParameters NebulaStencil = new()
    {
        Enabled = true,
        Ref = 1,
        Op = StencilOp.Keep,
        Func = StencilFunc.NotEqual,
    };

    public override OverlaySpace Space =>
        OverlaySpace.WorldSpaceBelowWorld |
        OverlaySpace.WorldSpaceBelowEntities |
        OverlaySpace.WorldSpaceBelowFOV;
    private readonly IClyde _clyde;
    private readonly IEntityManager _entManager;
    private readonly IMapManager _mapManager;
    private readonly IPrototypeManager _prototypes;
    private readonly IConfigurationManager _cfg;
    private readonly SharedTransformSystem _transform;
    private readonly SharedMapSystem _map;
    private readonly EntityLookupSystem _lookup;
    private readonly AmbientSpaceNebulaVisibility _visibility;
    private readonly IGameTiming _timing;
    private readonly Dictionary<(EntityUid Uid, AmbientSpaceLayer Layer), ShaderInstance> _fieldShaders = new();
    private readonly List<(EntityUid Uid, AmbientSpaceFieldComponent Field, TransformComponent Xform)> _fieldScratch = new();
    private List<Entity<MapGridComponent>> _grids = new();
    private bool _fieldsCacheValid;
    private GameTick _fieldsCacheTick;
    private MapId _fieldsCacheMapId;
    private Vector2 _fieldsCacheEyePos;
    private bool _stencilCacheValid;
    private GameTick _stencilCacheTick;
    private IClydeViewport? _stencilCacheViewport;
    private MapId _stencilCacheMapId;
    private Vector2i _stencilCacheSize;
    private Vector2 _stencilCacheBottomLeft;
    private Vector2 _stencilCacheTopRight;
    private IRenderTexture? _stencilTarget;

    public AmbientSpaceEffectOverlay(
        IEntityManager entManager,
        IPrototypeManager prototypes,
        IConfigurationManager cfg)
    {
        _entManager = entManager;
        _prototypes = prototypes;
        _cfg = cfg;
        _clyde = IoCManager.Resolve<IClyde>();
        _mapManager = IoCManager.Resolve<IMapManager>();
        _transform = entManager.System<SharedTransformSystem>();
        _map = entManager.System<SharedMapSystem>();
        _lookup = entManager.System<EntityLookupSystem>();
        _visibility = new AmbientSpaceNebulaVisibility(entManager, _mapManager, prototypes);
        _timing = IoCManager.Resolve<IGameTiming>();
        ZIndex = 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var quality = _cfg.GetCVar(CLVars.AmbientSpaceEffectsQuality);
        if (quality <= 0)
            return;

        var layer = args.Space switch
        {
            OverlaySpace.WorldSpaceBelowWorld => AmbientSpaceLayer.Lower,
            OverlaySpace.WorldSpaceBelowEntities => AmbientSpaceLayer.Mid,
            OverlaySpace.WorldSpaceBelowFOV => AmbientSpaceLayer.Upper,
            _ => (AmbientSpaceLayer?) null,
        };

        if (layer == null)
            return;

        if (!IsLayerEnabled(layer.Value))
            return;

        if (quality <= 1 && layer == AmbientSpaceLayer.Upper)
            return;

        var handle = args.WorldHandle;
        var eyePos = args.Viewport.Eye?.Position.Position ?? args.WorldAABB.Center;
        var qualityF = quality >= 2 ? 1f : 0f;
        var densityMul = Math.Clamp(_cfg.GetCVar(CLVars.AmbientSpaceEffectsDensity), 0f, 1.5f);
        var maxFields = quality >= 3 ? FixedMaxFields + 8 : FixedMaxFields;
        maxFields = Math.Max(1, (int) (maxFields * densityMul));

        EnsureFieldCache(args.MapId, eyePos);
        if (layer is AmbientSpaceLayer.Mid or AmbientSpaceLayer.Upper)
        {
            EnsureStencilTarget(args.Viewport.Size);
            EnsureStencilMask(args);
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(_prototypes.Index(StencilMaskShader).Instance());
            handle.DrawTextureRect(_stencilTarget!.Texture, args.WorldBounds);
            DrawFieldsDirect(args, layer.Value, eyePos, quality, qualityF, maxFields, densityMul);
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(null);
            return;
        }

        DrawFieldsDirect(args, layer.Value, eyePos, quality, qualityF, maxFields, densityMul);
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    private bool IsLayerEnabled(AmbientSpaceLayer layer)
    {
        return layer switch
        {
            AmbientSpaceLayer.Lower => _cfg.GetCVar(CLVars.AmbientSpaceLayerLower),
            AmbientSpaceLayer.Mid => _cfg.GetCVar(CLVars.AmbientSpaceLayerMid),
            AmbientSpaceLayer.Upper => _cfg.GetCVar(CLVars.AmbientSpaceLayerUpper),
            _ => true,
        };
    }

    private void EnsureStencilTarget(Vector2i size)
    {
        if (_stencilTarget?.Texture.Size == size)
            return;

        _stencilTarget?.Dispose();
        _stencilCacheValid = false;
        _stencilTarget = _clyde.CreateRenderTarget(
            size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            name: "ambient-nebula-stencil");
    }

    private void EnsureStencilMask(in OverlayDrawArgs args)
    {
        var tick = _timing.CurTick;
        var size = args.Viewport.Size;
        if (_stencilCacheValid &&
            _stencilCacheTick == tick &&
            ReferenceEquals(_stencilCacheViewport, args.Viewport) &&
            _stencilCacheMapId == args.MapId &&
            _stencilCacheSize == size &&
            _stencilCacheBottomLeft == args.WorldAABB.BottomLeft &&
            _stencilCacheTopRight == args.WorldAABB.TopRight)
        {
            return;
        }

        DrawStencilMask(args);
        _stencilCacheValid = true;
        _stencilCacheTick = tick;
        _stencilCacheViewport = args.Viewport;
        _stencilCacheMapId = args.MapId;
        _stencilCacheSize = size;
        _stencilCacheBottomLeft = args.WorldAABB.BottomLeft;
        _stencilCacheTopRight = args.WorldAABB.TopRight;
    }

    private void DrawStencilMask(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var invMatrix = args.Viewport.GetWorldToLocalMatrix();
        var mapId = args.MapId;
        var worldAABB = args.WorldAABB;
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        handle.RenderInRenderTarget(_stencilTarget!, () =>
        {
            _grids.Clear();
            _mapManager.FindGridsIntersecting(mapId, worldAABB, ref _grids);

            foreach (var grid in _grids)
            {
                var matrix = _transform.GetWorldMatrix(grid, xformQuery);
                var local = Matrix3x2.Multiply(matrix, invMatrix);
                handle.SetTransform(local);

                foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid, worldAABB))
                {
                    if (!_visibility.ShouldBlockNebula(tile))
                        continue;

                    var bounds = _lookup.GetLocalBounds(tile, grid.Comp.TileSize);
                    handle.DrawRect(bounds, Color.White);
                }
            }
        }, Color.Transparent);
    }

    private void DrawFieldsDirect(
        in OverlayDrawArgs args,
        AmbientSpaceLayer layer,
        Vector2 eyePos,
        int quality,
        float qualityF,
        int maxFields,
        float densityMul)
    {
        DrawFieldsCore(args.WorldHandle, layer, eyePos, quality, qualityF, maxFields, densityMul);
    }

    private void DrawFieldsCore(
        DrawingHandleWorld handle,
        AmbientSpaceLayer layer,
        Vector2 eyePos,
        int quality,
        float qualityF,
        int maxFields,
        float densityMul)
    {
        var drawn = 0;
        foreach (var (uid, field, xform) in _fieldScratch)
        {
            if (!_prototypes.TryIndex<AmbientSpaceEffectPrototype>(field.Effect, out var effect))
                continue;

            if (field.Seed == 0)
                continue;

            var fieldPos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);

            var parallax = layer switch
            {
                AmbientSpaceLayer.Lower => effect.LowerParallax,
                AmbientSpaceLayer.Mid => effect.MidParallax,
                _ => effect.UpperParallax,
            };

            var opacity = layer switch
            {
                AmbientSpaceLayer.Lower => effect.LowerOpacity,
                AmbientSpaceLayer.Mid => effect.MidOpacity,
                _ => effect.UpperOpacity,
            };

            if (quality <= 1)
                opacity *= 0.75f;
            else if (quality >= 3)
                opacity = MathF.Min(opacity * 1.08f, 1f);

            opacity *= densityMul;

            if (opacity <= 0f)
                continue;

            var drawPos = fieldPos + (eyePos - fieldPos) * (1f - parallax);
            var layerId = layer switch
            {
                AmbientSpaceLayer.Lower => 0f,
                AmbientSpaceLayer.Mid => 1f,
                _ => 2f,
            };
            var paletteColor = AmbientSpacePalette.ColorFromSeed(field.Seed);
            var shaderId = layer == AmbientSpaceLayer.Mid ? "AmbientNebulaMid" : effect.Shader;
            var shader = GetFieldShader(uid, layer, shaderId);
            var shaderSeed = AmbientSpacePalette.ShaderSeedFromField(field.Seed);
            var time = (float) _timing.CurTime.TotalSeconds + shaderSeed * 0.37f;
            var particleScale = quality switch
            {
                >= 3 => effect.ParticleScale * 1.1f,
                >= 2 => effect.ParticleScale,
                _ => effect.ParticleScale * 0.75f,
            };
            shader.SetParameter("nebula_color", paletteColor.WithAlpha(1f));
            shader.SetParameter("seed", shaderSeed);
            shader.SetParameter("density", field.Density);
            shader.SetParameter("layer_alpha", opacity);
            shader.SetParameter("particle_scale", particleScale);
            shader.SetParameter("quality", qualityF);
            shader.SetParameter("field_radius", radius);
            shader.SetParameter("layer_id", layerId);
            shader.SetParameter("time", time);
            shader.SetParameter("time_speed", effect.FlowSpeed);

            handle.UseShader(shader);
            handle.SetTransform(Matrix3Helpers.CreateTranslation(drawPos));
            handle.DrawTextureRect(Texture.White, Box2.CenteredAround(Vector2.Zero, new Vector2(radius * 2f, radius * 2f)));

            drawn++;
            if (drawn >= maxFields)
                break;
        }
    }

    private void EnsureFieldCache(MapId mapId, Vector2 eyePos)
    {
        var tick = _timing.CurTick;
        if (_fieldsCacheValid &&
            _fieldsCacheTick == tick &&
            _fieldsCacheMapId == mapId &&
            _fieldsCacheEyePos == eyePos)
        {
            return;
        }

        CollectFields(mapId, eyePos);
        _fieldsCacheValid = true;
        _fieldsCacheTick = tick;
        _fieldsCacheMapId = mapId;
        _fieldsCacheEyePos = eyePos;
    }

    private void CollectFields(MapId mapId, Vector2 eyePos)
    {
        _fieldScratch.Clear();
        _scratchFieldUids.Clear();
        var cullBox = Box2.CenteredAround(eyePos, new Vector2(FixedVisualRange * 2f, FixedVisualRange * 2f));
        var query = _entManager.EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var fieldPos = _transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            var fieldBounds = _visibility.GetPotentialDrawBounds(field, fieldPos, eyePos, radius);
            if (!cullBox.Intersects(fieldBounds))
                continue;

            _fieldScratch.Add((uid, field, xform));
            _scratchFieldUids.Add(uid);
        }

        _fieldScratch.Sort((a, b) =>
        {
            var da = FieldPriority(eyePos, _transform.GetWorldPosition(a.Xform), MathF.Max(a.Field.Radius, 1f));
            var db = FieldPriority(eyePos, _transform.GetWorldPosition(b.Xform), MathF.Max(b.Field.Radius, 1f));
            return da.CompareTo(db);
        });

        PruneFieldShaders();
    }

    private static float FieldPriority(Vector2 eyePos, Vector2 fieldPos, float radius)
    {
        return (eyePos - fieldPos).Length() - radius;
    }

    private ShaderInstance GetFieldShader(EntityUid uid, AmbientSpaceLayer layer, string id)
    {
        var key = (uid, layer);
        if (_fieldShaders.TryGetValue(key, out var existing))
            return existing;

        ProtoId<ShaderPrototype> proto = string.IsNullOrEmpty(id) ? FallbackShader : id;
        if (!_prototypes.TryIndex(proto, out ShaderPrototype? shaderProto))
            shaderProto = _prototypes.Index(FallbackShader);

        var instance = shaderProto.InstanceUnique();
        if (layer is AmbientSpaceLayer.Mid or AmbientSpaceLayer.Upper)
            instance.Stencil = NebulaStencil;
        _fieldShaders[key] = instance;
        return instance;
    }

    private void PruneFieldShaders()
    {
        if (_fieldShaders.Count == 0)
            return;

        List<(EntityUid Uid, AmbientSpaceLayer Layer)>? toRemove = null;
        foreach (var key in _fieldShaders.Keys)
        {
            if (_entManager.EntityExists(key.Uid) && _scratchFieldUids.Contains(key.Uid))
                continue;

            toRemove ??= new List<(EntityUid, AmbientSpaceLayer)>();
            toRemove.Add(key);
        }

        if (toRemove == null)
            return;

        foreach (var key in toRemove)
        {
            _fieldShaders.Remove(key, out var shader);
            shader?.Dispose();
        }
    }
}
