using System.Numerics;
using Content.Shared._Crescent.ShipShields;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._Crescent.ShipShields;
public sealed class ShipShieldOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "PersonalShieldSkin"; // Lua personal shield to ship shield

    private readonly FixtureSystem _fixture;
    private readonly SharedPhysicsSystem _physics;
    private readonly IEntityManager _entManager;
    private readonly ShaderInstance _baseShader;
    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();
    private readonly HashSet<EntityUid> _seen = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public ShipShieldOverlay(IEntityManager entityManager, IPrototypeManager prototypeManager)
    {
        _entManager = entityManager;
        _fixture = _entManager.System<FixtureSystem>();
        _physics = _entManager.System<Robust.Client.Physics.PhysicsSystem>();
        _baseShader = prototypeManager.Index(ShaderId).Instance().Duplicate();

        ZIndex = 8;
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();
        foreach (var shader in _shaders.Values)
            shader.Dispose();
        _shaders.Clear();
        _baseShader.Dispose();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var handle = args.WorldHandle;
        _seen.Clear();

        var enumerator = _entManager.AllEntityQueryEnumerator<ShipShieldVisualsComponent, FixturesComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out var visuals, out var fixtures, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (visuals.Form <= 0f && visuals.Shatter <= 0f)
                continue;

            var fixture = _fixture.GetFixtureOrNull(uid, "shield", fixtures);
            if (fixture is not { Shape: ChainShape chain })
                continue;

            var transform = _physics.GetPhysicsTransform(uid, xform);
            if (!TryGetChainLocalBounds(chain, out var localBounds))
                continue;

            var worldCenter = Transform.Mul(transform, localBounds.Center);
            var worldBounds = Box2.CenteredAround(worldCenter, localBounds.Size);
            var cullPad = MathF.Max(localBounds.Width, localBounds.Height) * 0.15f;
            worldBounds = worldBounds.Enlarged(cullPad);
            if (!args.WorldAABB.Intersects(worldBounds))
                continue;

            var size = localBounds.Size;
            if (size.X <= 0f || size.Y <= 0f)
                continue;
            var hexDensity = visuals.HexDensity;
            var pixelGrid = visuals.PixelGrid;

            var color = visuals.ShieldColor;
            if (color.A >= 1f) color = color.WithAlpha(0.92f);

            var shader = GetShader(uid);
            _seen.Add(uid);

            // Lua personal shield to ship shield start
            shader.SetParameter("progress", GetProgress(visuals));
            shader.SetParameter("skin_color", color);
            shader.SetParameter("brightness", visuals.Brightness);
            shader.SetParameter("pixel_grid", pixelGrid);
            shader.SetParameter("hex_density", hexDensity);
            shader.SetParameter("form_origin", visuals.FormOrigin);
            shader.SetParameter("fill_level", visuals.FillLevel);
            shader.SetParameter("line_level", visuals.LineLevel);
            shader.SetParameter("rim_level", visuals.RimLevel);
            shader.SetParameter("core_fade", visuals.CoreFade);
            shader.SetParameter("shard_scale", visuals.ShardScale);
            shader.SetParameter("alpha_bands", visuals.AlphaBands);
            shader.SetParameter("breath_depth", visuals.BreathDepth);

            handle.UseShader(shader);
            var angle = new Angle(MathF.Atan2(transform.Quaternion2D.S, transform.Quaternion2D.C));
            handle.SetTransform(Matrix3Helpers.CreateTransform(transform.Position, angle));
            handle.DrawTextureRect(Texture.White, Box2.CenteredAround(Vector2.Zero, size));
            // Lua personal shield to ship shield end
        }

        PruneShaders();
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    private ShaderInstance GetShader(EntityUid uid)
    {
        if (_shaders.TryGetValue(uid, out var existing))
            return existing;
        var shader = _baseShader.Duplicate();
        _shaders[uid] = shader;
        return shader;
    }

    private void PruneShaders()
    {
        if (_shaders.Count == _seen.Count)
            return;
        List<EntityUid>? remove = null;
        foreach (var uid in _shaders.Keys)
        {
            if (_seen.Contains(uid))
                continue;
            remove ??= new List<EntityUid>();
            remove.Add(uid);
        }

        if (remove == null)
            return;
        foreach (var uid in remove)
        {
            _shaders[uid].Dispose();
            _shaders.Remove(uid);
        }
    }

    private static float GetProgress(ShipShieldVisualsComponent visuals)
    {
        return visuals.Shatter > 0f ? 1f + MathF.Min(visuals.Shatter, 1f) : visuals.Form;
    }

    private static bool TryGetChainLocalBounds(ChainShape chain, out Box2 localBounds)
    {
        localBounds = default;
        if (chain.Count < 2) return false;

        localBounds = Box2.CenteredAround(chain.Vertices[0], Vector2.Zero);
        for (var i = 0; i < chain.Count; i++) localBounds = localBounds.ExtendToContain(chain.Vertices[i]);
        return localBounds.Width > 0f && localBounds.Height > 0f;
    }
}
