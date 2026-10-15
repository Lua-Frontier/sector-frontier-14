using Robust.Shared.GameStates;

namespace Content.Shared.Morgue.Components; // Lua Server > Shared

/// <summary>
/// used to track actively cooking crematoriums
/// </summary>
[RegisterComponent, NetworkedComponent] // Lua
public sealed partial class ActiveCrematoriumComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public float Accumulator = 0;
}
