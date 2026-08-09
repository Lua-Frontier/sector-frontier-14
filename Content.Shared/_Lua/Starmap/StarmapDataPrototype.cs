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

    [DataField]
    public string StarType = "beacon";

    [DataField]
    public Color? Color;

    [DataField]
    public string? Station;

    [DataField]
    public string? WorldgenConfig;

    [DataField]
    public string[] WorldgenConfigs = Array.Empty<string>();

    public IEnumerable<string> EnumerateWorldgenConfigs()
    {
        if (WorldgenConfigs.Length > 0)
        {
            foreach (var id in WorldgenConfigs)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    yield return id;
            }

            yield break;
        }

        if (!string.IsNullOrWhiteSpace(WorldgenConfig))
            yield return WorldgenConfig;
    }

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
    public string[] CoordinateDisks = Array.Empty<string>();

    [DataField]
    public string? Company;

    [DataField]
    public string[] VisibleCompanies = Array.Empty<string>();

    [DataField]
    public bool VisibleToAll;

    [DataField]
    public bool ExcludeFromGlobalUnlock;
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
