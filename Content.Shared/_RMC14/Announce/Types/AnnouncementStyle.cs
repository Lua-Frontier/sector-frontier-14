namespace Content.Shared._RMC14.Announce;

public sealed class AnnouncementStyle
{
    public AnnouncementAnimationConfig AnimationConfig { get; set; } = new();
    public AnnouncementLayoutConfig LayoutConfig { get; set; } = new();
    public AnnouncementBackgroundConfig BackgroundConfig { get; set; } = new();
    public AnnouncementTextConfig TextConfig { get; set; } = new();
    public AnnouncementSpriteConfig SpriteConfig { get; set; } = new();
    public AnnouncementTitleConfig TitleConfig { get; set; } = new();
    public AnnouncementScalingConfig ScalingConfig { get; set; } = new();
}
