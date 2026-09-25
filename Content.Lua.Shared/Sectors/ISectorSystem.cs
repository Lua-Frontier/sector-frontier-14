// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using Content.Lua.Shared.Starmap;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Lua.Shared.Sectors;

public interface ISectorSystem : IEntitySystem
{
    float EmergencyShuttleIndex { get; set; }
    bool CentComStarUnlocked { get; }
    bool BootstrapComplete { get; }
    bool TryGetMapId(string configId, out MapId mapId);
    bool TryGetHubMapId(out MapId mapId);
    MapId ResolveHubMapId();
    string? GetHubSectorId();
    bool TryGetSectorId(MapId mapId, out string sectorId);
    bool TryGetStationGrid(string configId, out EntityUid stationGrid);
    bool TryGetSectorConfig(MapId mapId, out StarDefinition config);
    bool TryGetCentComSectorId(out string sectorId);
    bool TryGetCentComMapId(out MapId mapId);
    bool TryGetCentCom(out EntityUid mapUid, out MapId mapId, out EntityUid grid);
    EntityUid? GetCentComMapUid();
    void UnlockCentComFtl();
    string GetSectorDisplayName(MapId mapId);
    List<MapId> GetDeadDropMapIds();
    IEnumerable<(string Id, MapId MapId, EntityUid MapUid)> EnumerateSectorMaps();
    void StartAllAutoStartSectors();
}
