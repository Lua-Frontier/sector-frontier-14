namespace Content.Shared._Lua.FtlPoints.Components;

[RegisterComponent]
public sealed partial class WarpDriveComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Fuel = 90;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int FuelPerJump = 30;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Charge;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int ChargeNeeded = 30;

    [DataField, ViewVariables]
    public bool Charging;
}
