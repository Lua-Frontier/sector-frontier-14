using System;
using Robust.Shared.GameStates;

namespace Content.Shared._NF.Shuttles.Components;

/// <summary>
/// This is a stub component for allowing/denying FTL on a shuttle.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShuttleFTLComponent : Component
{
	[AutoNetworkedField]
	public bool InCombat;

	public TimeSpan CombatUntil = TimeSpan.Zero;
	public TimeSpan CombatCooldown = TimeSpan.FromSeconds(120);
}
