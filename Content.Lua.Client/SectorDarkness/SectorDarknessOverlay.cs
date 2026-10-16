using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Lua.Client.SectorDarkness;

public sealed class SectorDarknessOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "SectorDarkness";

    private readonly ShaderInstance _shader;

    public float Strength;
    public MapId Map = MapId.Nullspace;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public SectorDarknessOverlay(IPrototypeManager prototype)
    {
        _shader = prototype.Index(Shader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Strength > 0f && args.MapId == Map;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("strength", Strength);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
