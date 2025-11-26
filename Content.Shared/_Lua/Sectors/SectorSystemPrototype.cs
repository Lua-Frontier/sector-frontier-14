// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Shared._Lua.Sectors;

[Prototype("sectorSystemPrototype")]
public sealed partial class SectorSystemPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public string Station = string.Empty;

    [DataField]
    public string? WorldgenConfig;

    [DataField]
    public bool AddFtlDestination = true;

    [DataField]
    public string[]? FtlWhitelist;

    [DataField]
    public string? RequiredGamePreset;

    [DataField]
    public string[]? RequiredGamePresets;

    [DataField]
    public string? DefaultGamePreset;

    [DataField]
    public bool AutoStart = false;

    [DataField]
    public string[] ParallaxPool = Array.Empty<string>();

    [DataField("poiGroups")]
    public SectorPOIGroup[] POIGroups = Array.Empty<SectorPOIGroup>();

    [DataField]
    public bool DeadDropEnabled = true;

    [DataField]
    public int DeadDropCount = 2;

    [DataField]
    public Vector2? StarmapPosition;

    [DataField]
    public Color? StarmapColor;
}

[DataDefinition]
public sealed partial class SectorPOIGroup
{
    [DataField(required: true)]
    public string Group = string.Empty;

    [DataField]
    public int Count = 0;

    [DataField]
    public bool Ring;
}


