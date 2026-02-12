namespace Content.Server._Goobstation.SpaceWhale;

[RegisterComponent]
public sealed partial class TailedEntitySegmentComponent : Component
{
    [DataField]
    public EntityUid HeadEntity;

    [DataField]
    public int Index;
}

