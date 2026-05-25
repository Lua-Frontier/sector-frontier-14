// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client._Lua.Actions.Systems;

namespace Content.Client._Lua.Actions.Components;

[RegisterComponent, Access(typeof(SoftNightVisionSystem))]
public sealed partial class SoftNightVisionLightComponent : Component
{
    [ViewVariables]
    public float FinalEnergy;

    [ViewVariables]
    public float TimeForFinalEnergy;

    [ViewVariables]
    public float TimePassed;

    [ViewVariables]
    public bool Increase;
}
