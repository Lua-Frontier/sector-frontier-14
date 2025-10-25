// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Starmap.Components;

[RegisterComponent]
public sealed partial class CoordinatesDiskMergerComponent : Component
{
    [DataField]
    public string SlotA = "disk_a";

    [DataField]
    public string SlotB = "disk_b";

    [DataField]
    public float MergeDurationSeconds = 10f;

    [ViewVariables]
    public bool IsMerging = false;

    [ViewVariables]
    public TimeSpan MergeStartedAt = TimeSpan.Zero;
}


