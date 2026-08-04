using Content.Shared.Actions;

namespace Content.Shared._Goobstation.Dash;

[RegisterComponent]
public sealed partial class DashActionComponent : Component
{
    [DataField]
    public string? ActionProto;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? ActionUid;
}

public sealed partial class DashActionEvent : WorldTargetActionEvent
{
    [DataField]
    public float Distance = 4.65f;

    [DataField]
    public float Speed = 9.65f;

    [DataField]
    public bool NeedsGravity = true;

    [DataField]
    public bool AffectedBySpeed = true;
}
