// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.EntityTable;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Content.Shared.Procedural;
using Content.Shared.Dataset;
using Content.Shared.Salvage.Expeditions.Modifiers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Stargate;

public enum MobSpawnMode : byte
{
    Surface,
    DungeonOnly,
    Both,
    None
}

[Prototype]
public sealed partial class StargatePlanetPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public List<ProtoId<BiomeTemplatePrototype>> Biome = new() { "Grasslands" };

    [DataField]
    public ProtoId<DungeonConfigPrototype>? DungeonConfig;

    [DataField]
    public List<ProtoId<DungeonConfigPrototype>>? DungeonConfigs;

    [DataField]
    public int DungeonCountMin = 1;

    [DataField]
    public int DungeonCountMax = 1;

    [DataField]
    public int DungeonDistanceMin = 50;

    [DataField]
    public int DungeonDistanceMax = 120;

    [DataField]
    public float WorldRadiusMin = 200f;

    [DataField]
    public float WorldRadiusMax = 200f;

    [DataField]
    public float GateSafeRadius = 8f;

    [DataField]
    public int DungeonMobCap = 128;

    [DataField]
    public int DungeonMobDensity = 8;

    [DataField]
    public int DungeonMobsPerRoomMin = 1;

    [DataField]
    public int DungeonMobsPerRoomMax = 6;

    [DataField]
    public ProtoId<EntityTablePrototype> DungeonMobTable = "LuaGateDungeonMobTable";

    [DataField]
    public MobSpawnMode MobSpawnMode = MobSpawnMode.Surface;

    [DataField]
    public int MobLayerCount = 1;

    [DataField]
    public List<ProtoId<BiomeMarkerLayerPrototype>> MobLayers = new();

    [DataField]
    public float RareSurfaceMobChance;

    [DataField]
    public int RareSurfaceMobLayerCount = 1;

    [DataField]
    public List<ProtoId<BiomeMarkerLayerPrototype>> RareSurfaceMobLayers = new();

    [DataField]
    public int LootLayerCount = 0;

    [DataField]
    public List<ProtoId<BiomeMarkerLayerPrototype>> LootLayers = new()
    {
        "OreIron",
        "OreQuartz",
        "OreCoal",
        "OreSalt",
        "OreGold",
        "OreSilver",
        "OrePlasma",
        "OreUranium",
        "GateGasDeposits",
        "OreDiamond",
        "OreArtifactFragment",
        "OreMagmite",
    };

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> NameDataset = "NamesBorer";

    [DataField]
    public float RestrictedRange = 200f;

    [DataField]
    public float Weight = 1f;

    [DataField]
    public List<ProtoId<SalvageAirMod>>? AirMods;

    [DataField]
    public List<ProtoId<SalvageTemperatureMod>>? TemperatureMods;

    [DataField]
    public List<ProtoId<SalvageLightMod>>? LightMods;

    [DataField]
    public List<ProtoId<SalvageWeatherMod>>? WeatherMods;

    [DataField]
    public List<ProtoId<PlanetQuest.PlanetQuestPrototype>> QuestPrototypes = new();
}
