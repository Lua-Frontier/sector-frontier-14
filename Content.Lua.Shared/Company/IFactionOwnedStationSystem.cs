using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Lua.Shared.Company;

public interface IFactionOwnedStationSystem : IEntitySystem
{
    bool TryGetOriginalOwner(EntityUid station, out string? companyId);
    string? GetSpawnAccessCompanies(EntityUid station);
    void SetOwner(EntityUid station, string? companyId);
    void BuildMapOwnership(Dictionary<MapId, string> ownerByMap);
}
