using System.Numerics;
using Content.Shared._Lua.Announce;
using Content.Shared._RMC14.Announce;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Announce;

public sealed class AnnouncementDisplayData
{
    public AnnouncementPreset AnnouncementId { get; set; }
    public string[] Text { get; set; } = Array.Empty<string>();
    public float Priority { get; set; }
    public AnnouncementPresentation Presentation { get; set; } = new();
    public NetEntity? SpeakerEntity { get; set; }
    public string? SpeakerName { get; set; }
    public string? SpeakerJobTitle { get; set; }
    public uint OverrideId { get; set; }
    public Vector2? ScreenPositionOverride { get; set; }
    public float LayoutScale { get; set; } = 1f;
    public bool? ShowTitleOverride { get; set; }
    public bool? ShowSpriteOverride { get; set; }
    public Color? TextColorOverride { get; set; }
    public Color? TitleColorOverride { get; set; }
    public Color? SpriteBoxColorOverride { get; set; }
    public Color? SpriteBoxBorderColorOverride { get; set; }
    public Color? CRTGlowColorOverride { get; set; }
    public Color? BackgroundColorOverride { get; set; }
    public float? BodyTextScaleOverride { get; set; }
    public float? TitleTextScaleOverride { get; set; }
    public string? TitleOverride { get; set; }
    public string? DecalRsiOverride { get; set; }
    public string? DecalStateOverride { get; set; }

    public AnnouncementStyle Style => Presentation.Style;
    public bool SupportsSpriteCardOverride => Presentation.ShowSprite && Presentation.Style.SpriteConfig.ShowSpriteBox;
    public bool ShowSprite => SupportsSpriteCardOverride
        ? ShowSpriteOverride ?? Presentation.ShowSprite
        : Presentation.ShowSprite;
    public bool IsFactionFlagPortrait =>
        !string.IsNullOrWhiteSpace(DecalRsi) &&
        !string.IsNullOrWhiteSpace(DecalState) &&
        SpeakerEntity == null;
    public string? DecalRsi => DecalRsiOverride;
    public string? DecalState => DecalStateOverride;
    public string? PortraitPrototype => Presentation.PortraitPrototype;
    public string? PortraitRsi => Presentation.PortraitRsi;
    public string? PortraitState => Presentation.PortraitState;
    public bool TintPortrait => Presentation.TintPortrait;
    public string Title
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TitleOverride))
                return TitleOverride;

            var title = Presentation.Style.TitleConfig.Title;
            return title is null || string.IsNullOrEmpty(title.Value.Id) ? string.Empty : Loc.GetString(title.Value);
        }
    }
}
