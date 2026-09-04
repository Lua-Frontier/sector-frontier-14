using System.Numerics;
using Content.Shared._RMC14.Announce.Animations;
using Robust.Shared.Maths;

namespace Content.Shared._RMC14.Announce;

public sealed class AnnouncementAnimationConfig
{
    public IAnnouncementAnimationConfig Animation { get; set; } = new TypewriterAnimationConfig();
    public float HoldDuration { get; set; } = 3f;
    public float FadeOutDuration { get; set; } = 0.5f;
    public float FlickerChance { get; set; } = 0.01f;
    public bool EnableCRT { get; set; } = false;
    public CRTSettings? CRTSettings { get; set; }
}

public sealed class AnnouncementLayoutConfig
{
    public AnnouncementPosition Position { get; set; } = AnnouncementPosition.MiddleCenter;
    public AnnouncementSpeakerNamePosition SpeakerNamePosition { get; set; } = AnnouncementSpeakerNamePosition.Below;
    public AnnouncementSpritePosition SpritePosition { get; set; } = AnnouncementSpritePosition.Left;
    public float SpriteSpacing { get; set; } = 20f;
    public SpriteDisplayMode SpriteDisplayMode { get; set; } = SpriteDisplayMode.TopHalf;
    public AnnouncementTitlePosition TitlePosition { get; set; } = AnnouncementTitlePosition.Above;
}

public sealed class AnnouncementBackgroundConfig
{
    public bool ShowBackground { get; set; } = true;
    public float BackgroundAlpha { get; set; } = 0.8f;
    public Color BackgroundColor { get; set; } = Color.Black;
}

public sealed class AnnouncementTextConfig
{
    public Color PrimaryColor { get; set; } = Color.White;
    public string Font { get; set; } = "Default";
    public float FontSize { get; set; } = 16f;
    public float LineHeight { get; set; } = 40f;
    public bool ShowSpeakerName { get; set; } = true;
    public Color SpeakerNameColor { get; set; } = Color.White;
    public float SpeakerNameFontSize { get; set; } = 12f;
}

public sealed class AnnouncementSpriteConfig
{
    public bool ShowSpriteBox { get; set; } = true;
    public Color SpriteBoxColor { get; set; } = Color.Black;
    public Color SpriteBoxBorderColor { get; set; } = Color.White;
    public float SpriteBoxBorderThickness { get; set; } = 2f;
    public float SpriteBoxPadding { get; set; } = 10f;
    public string? SpriteBoxShader { get; set; }
    public bool SpriteGlow { get; set; }
    public Color SpriteGlowColor { get; set; } = Color.White;
    public float SpriteGlowIntensity { get; set; } = 0.5f;
    public float SpriteScale { get; set; } = 1f;
    public float SpriteCardOpacity { get; set; } = 0.8f;
    public Vector2 SpriteOffset { get; set; } = Vector2.Zero;
}

public sealed class AnnouncementTitleConfig
{
    public bool ShowTitle { get; set; }
    public LocId? Title { get; set; }
    public string TitleFont { get; set; } = "DefaultBold";
    public Color TitleColor { get; set; } = Color.White;
    public float TitleFontSize { get; set; } = 20f;
    public bool TitleUnderline { get; set; }
    public float TitleUnderlineThickness { get; set; } = 2f;
}

public sealed class AnnouncementScalingConfig
{
    public bool EnableResponsiveScaling { get; set; } = true;
    public float ResponsiveScaleFactor { get; set; } = 1f;
    public float MinScale { get; set; } = 0.5f;
    public float MaxScale { get; set; } = 2f;
}

public sealed class CRTSettings
{
    public bool Enabled { get; set; } = true;

    // Scanline Settings
    public bool ShowScanlines { get; set; } = true;
    public float ScanlineSpacing { get; set; } = 4f;
    public float ScanlineThickness { get; set; } = 1.3f;
    public float ScanlineAlpha { get; set; } = 0.3f;
    public float ScanlineSpeed { get; set; } = 60f;
    public Color ScanlineColor { get; set; } = Color.Black;
    public float ScanlineWaveFrequency { get; set; } = 3f;
    public float ScanlineWaveAmplitude { get; set; } = 1.5f;
    public float ScanlineFlickerIntensity { get; set; } = 0.5f;
    public float ScanlineFlickerSpeed { get; set; } = 2f;
    public float ScanlineGlitchChance { get; set; } = 0.02f;
    public Color ScanlineGlitchColor { get; set; } = Color.FromHex("#00ff00");
    public float ScanlineGlitchAlpha { get; set; } = 0.15f;

    // Noise Settings
    public bool ShowNoise { get; set; } = true;
    public float NoiseIntensity { get; set; } = 0.5f;
    public float NoiseAlpha { get; set; } = 0.4f;
    public float NoiseUpdateFrequency { get; set; } = 0.08f;
    public float NoiseMinSize { get; set; } = 0.5f;
    public float NoiseMaxSize { get; set; } = 2f;
    public float NoiseStaticChance { get; set; } = 0.09f;
    public float NoiseStaticMinWidth { get; set; } = 1f;
    public float NoiseStaticMaxWidth { get; set; } = 3f;
    public float NoiseStaticMinHeight { get; set; } = 3f;
    public float NoiseStaticMaxHeight { get; set; } = 11f;
    public float NoiseStaticAlpha { get; set; } = 0.3f;

    // Vignette Settings
    public bool ShowVignette { get; set; } = true;
    public float VignetteIntensity { get; set; } = 0.6f;
    public Color VignetteColor { get; set; } = Color.Black;
    public float VignetteSizeMultiplier { get; set; } = 0.15f;
    public float VignetteAlphaMultiplier { get; set; } = 0.4f;
    public float VignettePulseSpeed { get; set; } = 1.5f;
    public float VignettePulseAmplitude { get; set; } = 0.1f;
    public float VignetteCornerSize { get; set; } = 0.7f;
    public float VignetteEdgeAlpha { get; set; } = 0.6f;

    // Flash Tint Settings
    public Color GlowColor { get; set; } = Color.FromHex("#00ff41");

    // Chromatic Aberration Settings
    public bool ShowChromaticAberration { get; set; } = false;
    public float ChromaticAmount { get; set; } = 2f;
    public int ChromaticParticleCount { get; set; } = 5;
    public float ChromaticParticleChance { get; set; } = 0.3f;
    public float ChromaticParticleMinSize { get; set; } = 2f;
    public float ChromaticParticleMaxSize { get; set; } = 6f;
    public float ChromaticParticleAlpha { get; set; } = 0.3f;
    public float ChromaticAnimationSpeed { get; set; } = 2f;

    // Flicker/Flash Settings
    public float FlickerThreshold { get; set; } = 0.9f;
    public float FlickerChance { get; set; } = 0.05f;
    public float FlickerAlpha { get; set; } = 0.02f;
    public Color FlickerColor { get; set; } = Color.White;
    public float FlashChance { get; set; } = 0.01f;
    public float FlashMaxBrightness { get; set; } = 0.05f;
}
