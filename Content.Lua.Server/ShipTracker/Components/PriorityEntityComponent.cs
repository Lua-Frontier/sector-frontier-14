namespace Content.Lua.Server.ShipTracker.Components;

[RegisterComponent]
public sealed partial class PriorityEntityComponent : Component
{
    [DataField("priority")] public int Priority = 1;
}
