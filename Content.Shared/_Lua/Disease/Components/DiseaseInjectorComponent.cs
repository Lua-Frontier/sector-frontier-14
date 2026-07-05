// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Disease.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DiseaseInjectorComponent : Component
{
    [DataField("sampleSlot")]
    public string SampleSlotID = "sample_slot";

    [DataField]
    public TimeSpan InjectTime = TimeSpan.FromSeconds(20);

    [DataField]
    public int UsesLeft = 1;
}
