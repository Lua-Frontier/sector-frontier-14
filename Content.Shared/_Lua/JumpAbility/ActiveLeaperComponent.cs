// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.JumpAbility;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveLeaperComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownDuration;
}
