namespace Content.Lua.Server.ShipTracker.Rules.EndOnShipDestruction;

[RegisterComponent]
public sealed partial class EndOnShipDestructionComponent : Component
{
    public EntityUid MainShip = default!;
}
