using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Announce;

[Serializable, NetSerializable]
public enum AnnouncementPosition : byte
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
    FullScreen
}

[Serializable, NetSerializable]
public enum AnnouncementDisplayPreference
{
    Stylized = 0,
    Simplified = 1,
    Disabled = 2,
    Default = 3
}

[Serializable, NetSerializable]
public enum AnnouncementState : byte
{
    Animating,
    Holding,
    FadingOut
}

[Serializable, NetSerializable]
public enum AnnouncementSpritePosition : byte
{
    Left,
    Right,
    Above,
    Below
}

[Serializable, NetSerializable]
public enum AnnouncementSpeakerNamePosition : byte
{
    Above,
    Below
}

[Serializable, NetSerializable]
public enum AnnouncementTitlePosition : byte
{
    Above,
    Below
}

[Serializable, NetSerializable]
public enum SpriteDisplayMode : byte
{
    TopHalf,
    FullSprite
}
