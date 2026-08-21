// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using System.Numerics;

namespace Content.Shared._Lua.Starmap;

[Prototype("starmapData")]
public sealed partial class StarmapDataPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<StarmapDataPrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField("stars")]
    [AlwaysPushInheritance]
    public StarDefinition[] Stars = Array.Empty<StarDefinition>();

    [DataField("hyperlanes")]
    [AlwaysPushInheritance]
    public string[][] Hyperlanes = Array.Empty<string[]>();

    [DataField("factionZones")]
    [AlwaysPushInheritance]
    public FactionZoneDefinition[] FactionZones = Array.Empty<FactionZoneDefinition>();

    [DataField("chartRegions")]
    [AlwaysPushInheritance]
    public ChartRegionDefinition[] ChartRegions = Array.Empty<ChartRegionDefinition>();

    [DataField("chartMarkers")]
    [AlwaysPushInheritance]
    public ChartMarkerDefinition[] ChartMarkers = Array.Empty<ChartMarkerDefinition>();
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
    public bool IsHub;

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

    [DataField]
    public string? Description;

    [DataField]
    public string? DescriptionFull;
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

[DataDefinition]
public sealed partial class FactionZoneDefinition
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public string Name = string.Empty;

    [DataField(required: true)]
    public Color Color = Color.White;

    [DataField(required: true)]
    public Vector2[] Points = Array.Empty<Vector2>();

    [DataField]
    public float FillAlpha = 0.08f;

    [DataField]
    public float BorderAlpha = 0.85f;

    [DataField]
    public bool ShowLabel = true;

    [DataField]
    public string? IconCompany;

    [DataField]
    public string? IconPath;

    [DataField]
    public string[] VisibleCompanies = Array.Empty<string>();

    [DataField]
    public bool VisibleToAll = true;

    [DataField]
    public bool ExcludeFromGlobalUnlock;

    [DataField]
    public string? Description;

    [DataField]
    public string? DescriptionFull;
}

[DataDefinition]
public sealed partial class ChartRegionDefinition
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public Color Color = Color.White;

    [DataField(required: true)]
    public Vector2[] Points = Array.Empty<Vector2>();

    [DataField]
    public float FillAlpha = 0.04f;

    [DataField]
    public float BorderAlpha = 0.75f;

    [DataField]
    public bool Dashed = true;

    [DataField]
    public float DashLength = 8f;

    [DataField]
    public float GapLength = 5f;

    [DataField]
    public bool ShowLabel = true;
}

[DataDefinition]
public sealed partial class ChartMarkerDefinition
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public Vector2 Position = Vector2.Zero;

    [DataField]
    public Vector2? EndPosition;

    [DataField]
    public string? LinkTo;

    [DataField]
    public string? LinkLabel;

    [DataField]
    public string Kind = "marker";

    [DataField]
    public Color Color = Color.FromHex("#8932B8");

    [DataField]
    public float Size = 8f;

    [DataField]
    public bool ShowLabel = true;
}
