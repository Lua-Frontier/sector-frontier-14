namespace Content.Lua.Server.ShipTracker.Components;

[RegisterComponent]
public sealed partial class ShipTrackerComponent : Component
{
    [DataField("faction")]
    [ViewVariables(VVAccess.ReadWrite)]
    public string Faction = "IndependentShip";

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Destroyed = false;

    [ViewVariables(VVAccess.ReadOnly)]
    public float SecondsWithoutPiloting = 0;

    [ViewVariables(VVAccess.ReadOnly)]
    public float CallDestroyedSeconds = 10;
}
