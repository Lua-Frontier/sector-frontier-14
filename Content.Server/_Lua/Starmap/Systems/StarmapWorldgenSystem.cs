// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Linq;
using Content.Server._Lua.AmbientSpaceEffects;
using Content.Server._Lua.Sectors;
using Content.Server._Lua.SpaceHazards;
using Content.Server.Backmen.Arrivals;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Worldgen.Prototypes;
using Content.Shared._Lua.Starmap;
using Content.Shared.Lua.CLVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class StarmapWorldgenSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly ISerializationManager _ser = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SectorSystem _sectors = default!;
    [Dependency] private readonly CentcommSystem _centcomm = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting, after: [typeof(SectorSystem)]);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        ApplyPendingWorldgen();
    }

    public void ApplyPendingWorldgen()
    {
        var dataId = _cfg.GetCVar(CLVars.StarmapDataId);
        if (!_protos.TryIndex<StarmapDataPrototype>(dataId, out var data))
            return;

        foreach (var def in data.Stars)
        {
            var configs = def.EnumerateWorldgenConfigs().ToArray();
            if (configs.Length == 0)
                continue;

            if (!TryResolveMap(def, out var mapId) || mapId == MapId.Nullspace)
                continue;

            if (!_map.TryGetMap(mapId, out var mapUid))
                continue;

            TryApplyWorldgen(mapUid.Value, configs);
        }
    }

    public bool TryApplyWorldgen(EntityUid mapUid, params string[] worldgenConfigIds)
    {
        if (worldgenConfigIds.Length == 0)
            return false;

        foreach (var configId in worldgenConfigIds)
        {
            if (!_protos.HasIndex<WorldgenConfigPrototype>(configId))
            {
                Log.Error($"Starmap worldgenConfig '{configId}' not found for map {ToPrettyString(mapUid)}");
                return false;
            }
        }

        WorldgenConfigPrototype.ApplyMany(mapUid, worldgenConfigIds, _protos, _ser, EntityManager);
        EntityManager.System<AmbientSpaceFieldPlacerSystem>().InitializePlacer(mapUid);
        EntityManager.System<SectorCelestialPlacerSystem>().InitializePlacer(mapUid);
        EntityManager.System<SectorBackgroundPlanetPlacerSystem>().InitializePlacer(mapUid);
        Log.Info($"Applied worldgenConfig(s) [{string.Join(", ", worldgenConfigIds)}] to {ToPrettyString(mapUid)}");
        return true;
    }

    private bool TryResolveMap(StarDefinition def, out MapId mapId)
    {
        mapId = MapId.Nullspace;

        if (string.Equals(def.StarType, "frontier", StringComparison.OrdinalIgnoreCase)
            || string.Equals(def.Id, "FrontierSector", StringComparison.OrdinalIgnoreCase))
        {
            mapId = _ticker.DefaultMap;
            return _mapManager.MapExists(mapId);
        }

        if (string.Equals(def.StarType, "centcom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(def.Id, "CentCom", StringComparison.OrdinalIgnoreCase))
        {
            mapId = _centcomm.CentComMap;
            return mapId != MapId.Nullspace && _mapManager.MapExists(mapId);
        }

        return _sectors.TryGetMapId(def.Id, out mapId);
    }
}
