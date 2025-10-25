// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Starmap;

[NetSerializable, Serializable]
public enum DiskMergerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DiskMergerBuiState : BoundUserInterfaceState
{
    public bool IsMerging;
    public float Progress01;
    public bool HasDiskA;
    public bool HasDiskB;
    public string DiskAName;
    public string DiskBName;
    public string[] DiskASectors;
    public string[] DiskBSectors;

    public DiskMergerBuiState(bool isMerging, float progress01, bool hasDiskA, bool hasDiskB, string diskAName, string diskBName, string[] diskASectors, string[] diskBSectors)
    {
        IsMerging = isMerging;
        Progress01 = progress01;
        HasDiskA = hasDiskA;
        HasDiskB = hasDiskB;
        DiskAName = diskAName;
        DiskBName = diskBName;
        DiskASectors = diskASectors;
        DiskBSectors = diskBSectors;
    }
}


