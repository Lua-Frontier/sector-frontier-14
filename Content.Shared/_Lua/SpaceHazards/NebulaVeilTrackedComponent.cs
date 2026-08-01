// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.SpaceHazards;

[RegisterComponent, NetworkedComponent]
public sealed partial class NebulaVeilTrackedComponent : Component
{
	public bool AddedStealth;
	public bool PreviousEnabled;
	public float PreviousVisibility;
}
