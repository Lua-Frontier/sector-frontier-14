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
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public ShipShieldOverlay(IEntityManager entityManager, IPrototypeManager prototypeManager)
    {
        _entManager = entityManager;
        _fixture = _entManager.System<FixtureSystem>();
        _physics = _entManager.System<Robust.Client.Physics.PhysicsSystem>();
        _shader = prototypeManager.Index(ShaderId).InstanceUnique();

        ZIndex = 8;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var handle = args.WorldHandle;

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

            // Lua personal shield to ship shield start
            _shader.SetParameter("progress", GetProgress(visuals));
            _shader.SetParameter("skin_color", color);
            _shader.SetParameter("brightness", visuals.Brightness);
            _shader.SetParameter("pixel_grid", pixelGrid);
            _shader.SetParameter("hex_density", hexDensity);
            _shader.SetParameter("form_origin", visuals.FormOrigin);
            _shader.SetParameter("fill_level", visuals.FillLevel);
            _shader.SetParameter("line_level", visuals.LineLevel);
            _shader.SetParameter("rim_level", visuals.RimLevel);
            _shader.SetParameter("core_fade", visuals.CoreFade);
            _shader.SetParameter("shard_scale", visuals.ShardScale);
            _shader.SetParameter("alpha_bands", visuals.AlphaBands);
            _shader.SetParameter("breath_depth", visuals.BreathDepth);

            handle.UseShader(_shader);
            var angle = new Angle(MathF.Atan2(transform.Quaternion2D.S, transform.Quaternion2D.C));
            handle.SetTransform(Matrix3Helpers.CreateTransform(transform.Position, angle));
            handle.DrawTextureRect(Texture.White, Box2.CenteredAround(Vector2.Zero, size));
            // Lua personal shield to ship shield end
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
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
