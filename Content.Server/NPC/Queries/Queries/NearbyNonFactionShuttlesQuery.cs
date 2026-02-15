using Content.Shared.Whitelist;
using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Queries.Queries;

/// <summary>
/// Returns nearby shuttles that do NOT belong to any of the excluded companies.
/// The "shuttles" are represented by entities with <see cref="Content.Server._Mono.NPC.HTN.ShipNpcTargetComponent"/>,
/// but company filtering is performed on the target's grid (GridUid), not on the ShipNpcTarget entity itself.
/// </summary>
public sealed partial class NearbyNonFactionShuttlesQuery : UtilityQuery
{
    [DataField]
    public float Range = 2000f;

    [DataField]
    public EntityWhitelist Blacklist = new();

    [DataField]
    public HashSet<ProtoId<CompanyPrototype>> ExcludedCompanies = new();
}

