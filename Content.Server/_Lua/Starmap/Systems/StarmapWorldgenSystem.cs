// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Linq;
using Content.Server._Lua.AmbientSpaceEffects;
using Content.Server._Lua.Sectors;
using Content.Server._Lua.SpaceHazards;
using Content.Server.Worldgen.Systems.Debris;
using Content.Server.Worldgen.Prototypes;
using Content.Shared._Lua.Starmap;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class StarmapWorldgenSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly ISerializationManager _ser = default!;
    [Dependency] private readonly DebrisPregenSystem _debrisPregen = default!;

    public bool TryApplyWorldgen(EntityUid mapUid, Action<string>? announceProgress = null, params string[] worldgenConfigIds)
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
        announceProgress?.Invoke(Loc.GetString("sector-startup-nebulas"));
        EntityManager.System<AmbientSpaceFieldPlacerSystem>().InitializePlacer(mapUid);
        EntityManager.System<SectorCelestialPlacerSystem>().InitializePlacer(mapUid);
        EntityManager.System<SectorBackgroundPlanetPlacerSystem>().InitializePlacer(mapUid);
        announceProgress?.Invoke(Loc.GetString("sector-startup-debris"));
        _debrisPregen.InitializePlacer(mapUid);
        Log.Info($"Applied worldgenConfig(s) [{string.Join(", ", worldgenConfigIds)}] to {ToPrettyString(mapUid)}");
        return true;
    }
}
