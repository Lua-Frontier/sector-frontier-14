using Content.Server.Shuttles.Systems;
using Content.Shared.Dataset;
using Content.Shared.Procedural;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared._NF.Bank.Components;
using Robust.Shared.Map;

namespace Content.Server._Lua.AiShuttle;

[RegisterComponent, Access(typeof(AiShuttleSpawnRule), typeof(ShuttleSystem))]
public sealed partial class AiShuttleSpawnRuleComponent : Component
{
    [DataField(required: true)]
    public Dictionary<string, IAiShuttleSpawnGroup> Groups = new();

    [DataField]
    public Dictionary<SectorBankAccount, float> RewardAccounts = new();

    [DataField]
    public List<EntityUid> GridsUid = new();
    public List<MapId> MapsUid = new();

    [DataField]
    public bool DeleteGridsOnEnd = true;
    public double StartingValue = 0;

    [DataField]
    public bool Asteroid = false;
}

public interface IAiShuttleSpawnGroup
{
    public float MinimumDistance { get; }
    public float MaximumDistance { get; }
    public List<LocId> NameLoc { get; }
    public ProtoId<LocalizedDatasetPrototype>? NameDataset { get; }
    public AiShuttleDatasetNameType NameDatasetType { get; set; }
    int MinCount { get; set; }
    int MaxCount { get; set; }
    public ComponentRegistry AddComponents { get; set; }
    public bool NameGrid { get; set; }
    public bool NameWarp { get; set; }
    public bool HideWarp { get; set; }
}

public enum AiShuttleDatasetNameType
{
    FTL,
    Nanotrasen,
    Verbatim,
}

[DataRecord]
public sealed class AiShuttleDungeonSpawnGroup : IAiShuttleSpawnGroup
{
    public List<ProtoId<DungeonConfigPrototype>> Protos = new();
    public float MinimumDistance { get; }

    public float MaximumDistance { get; }
    public List<LocId> NameLoc { get; } = new();
    public ProtoId<LocalizedDatasetPrototype>? NameDataset { get; }
    public AiShuttleDatasetNameType NameDatasetType { get; set; } = AiShuttleDatasetNameType.FTL;
    public int MinCount { get; set; } = 1;
    public int MaxCount { get; set; } = 1;
    public ComponentRegistry AddComponents { get; set; } = new();
    public bool NameGrid { get; set; } = false;
    public bool NameWarp { get; set; } = false;
    public bool HideWarp { get; set; } = false;

}

[DataRecord]
public sealed class AiShuttleGridSpawnGroup : IAiShuttleSpawnGroup
{
    public List<ResPath> Paths = new();
    public float MinimumDistance { get; }
    public float MaximumDistance { get; }
    public List<LocId> NameLoc { get; } = new();
    public ProtoId<LocalizedDatasetPrototype>? NameDataset { get; }
    public AiShuttleDatasetNameType NameDatasetType { get; set; } = AiShuttleDatasetNameType.FTL;
    public int MinCount { get; set; } = 1;
    public int MaxCount { get; set; } = 1;
    public ComponentRegistry AddComponents { get; set; } = new();
    public bool NameGrid { get; set; } = true;
    public bool NameWarp { get; set; } = true;
    public bool HideWarp { get; set; } = false;

}
