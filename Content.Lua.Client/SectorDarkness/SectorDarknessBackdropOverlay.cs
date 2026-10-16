using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Parallax.Managers;
using Content.Shared.CCVar;
using Content.Shared.Parallax;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Lua.Client.SectorDarkness;

public sealed class SectorDarknessBackdropOverlay : Overlay
{
    public const int BackdropZIndex = ParallaxSystem.ParallaxZIndex + 2;

    private static readonly ProtoId<ParallaxPrototype> Parallax = "SectorDarkness";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IParallaxManager _manager = default!;

    public float Strength;
    public MapId Map = MapId.Nullspace;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public SectorDarknessBackdropOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = BackdropZIndex;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Strength > 0f && args.MapId == Map;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);

        var parallax = _cfg.GetCVar(CCVars.ParallaxEnabled);
        if (parallax && Strength >= 0.5f)
        {
            var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
            var zoom = Math.Max(args.Viewport.Eye?.Zoom.X ?? 1f, 0.001f);
            ParallaxShaderHelper.Draw(
                handle,
                args.WorldAABB,
                _manager,
                _cfg,
                _manager.GetPrototype(Parallax),
                (float) _timing.RealTime.TotalSeconds,
                position,
                zoom / EyeManager.PixelsPerMeter,
                zoom);
        }

        var cover = parallax ? 1f - MathF.Abs(Strength * 2f - 1f) : Strength;
        if (cover > 0f)
            handle.DrawRect(args.WorldAABB, Color.Black.WithAlpha(cover));
    }
}
