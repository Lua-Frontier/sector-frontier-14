// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Shared._Lua.Starmap;

[Prototype("starmapData")]
public sealed partial class StarmapDataPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField("stars")]
    public StarDefinition[] Stars = Array.Empty<StarDefinition>();

    [DataField("hyperlanes")]
    public string[][] Hyperlanes = Array.Empty<string[]>();
}

[DataDefinition]
public sealed partial class StarDefinition
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public Vector2 Position = Vector2.Zero;

    /// <summary>
    /// sector | beacon | asteroid | ruin | warp | centcom | frontier
    /// </summary>
    [DataField]
    public string StarType = "beacon";

    [DataField]
    public Color? Color;

    [DataField]
    public string? Station;

    [DataField]
    public string? WorldgenConfig;

    [DataField]
    public string[] ParallaxPool = Array.Empty<string>();

    [DataField]
    public bool AutoStart;

    [DataField]
    public bool AddFtlDestination = true;

    [DataField]
    public string[]? FtlWhitelist;

    [DataField]
    public bool RequireCoordinateDisk;

    [DataField]
    public bool BeaconsOnly;

    [DataField]
    public string? RequiredGamePreset;

    [DataField]
    public string[]? RequiredGamePresets;

    [DataField]
    public string? DefaultGamePreset;

    [DataField("poiGroups")]
    public SectorPOIGroup[] POIGroups = Array.Empty<SectorPOIGroup>();

    [DataField]
    public bool DeadDropEnabled;

    [DataField]
    public int DeadDropCount = 2;

    [DataField]
    public bool BluespaceEventsEnabled = true;

    [DataField]
    public bool CrewMonitoringIsolated;

    [DataField]
    public bool Capturable;

    [DataField]
    public float CaptureDurationSeconds = 300f;

    [DataField]
    public string? DefaultFaction;

    [DataField]
    public string? DefaultFactionColor;

    [DataField]
    public string[] CoordinateDisks = Array.Empty<string>();

    [DataField]
    public string? Company;
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
