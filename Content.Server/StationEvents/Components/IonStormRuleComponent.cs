namespace Content.Server.StationEvents.Components;

/// <summary>
/// Gamerule component to mess up ai/borg laws when started.
/// </summary>
[RegisterComponent]
public sealed partial class IonStormRuleComponent : Component
{
    /// <summary>
    /// Lua: the storm hits one random sector from the list, hub if none are found.
    /// </summary>
    [DataField]
    public List<string> Sectors = new();
}
