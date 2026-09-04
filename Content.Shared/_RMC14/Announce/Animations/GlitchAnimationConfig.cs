namespace Content.Shared._RMC14.Announce.Animations;

public sealed class GlitchAnimationConfig : IAnnouncementAnimationConfig
{
    public float PrintSpeed { get; set; } = 0.03f;

    public float GlitchChance { get; set; } = 0.005f;

    public bool EnableVisualGlitch { get; set; } = true;
}
