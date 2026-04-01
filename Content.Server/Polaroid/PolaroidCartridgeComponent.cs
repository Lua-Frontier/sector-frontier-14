namespace Content.Server.DeadSpace.Polaroid;

[RegisterComponent]
public sealed partial class PolaroidCartridgeComponent : Component
{
    [DataField("maxAmount")]
    public int MaxAmount = 8;

    [DataField("currentAmount")]
    public int CurrentAmount = 8;
}
