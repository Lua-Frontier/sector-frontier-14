using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server.Salvage.JobBoard;

[RegisterComponent]
public sealed partial class SalvageJobsDataComponent : Component
{
    [DataField]
    public SortedDictionary<int, SalvageRankDatum> RankThresholds = new();

    [DataField]
    public SalvageRankDatum MaxRank;

    [DataField]
    public List<ProtoId<CargoBountyPrototype>> CompletedJobs = new();

    [DataField]
    public ProtoId<CargoAccountPrototype> RewardAccount = "Cargo";
}

[DataDefinition]
public partial record struct SalvageRankDatum
{
    [DataField]
    public LocId Title;

    [DataField]
    public ProtoId<CargoBountyGroupPrototype>? BountyGroup;

    [DataField]
    public ProtoId<CargoMarketPrototype>? UnlockedMarket;
}
