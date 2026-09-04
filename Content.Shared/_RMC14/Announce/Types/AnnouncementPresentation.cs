namespace Content.Shared._RMC14.Announce;

public sealed class AnnouncementPresentation
{
    public AnnouncementStyle Style { get; set; } = new();
    public bool ShowSprite { get; set; } = true;
    public string? PortraitPrototype { get; set; }
    public string? PortraitRsi { get; set; }
    public string? PortraitState { get; set; }
    public bool TintPortrait { get; set; }
}
