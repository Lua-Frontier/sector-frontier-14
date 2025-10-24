// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Starmap.Systems;
using Robust.Shared.Configuration;
using Content.Shared.Lua.CLVar;
using Robust.Shared.Map;
using Content.Shared._Lua.Starmap.Components;
using Content.Server.GameTicking;

namespace Content.Server._Lua.ShipTracker.Rules.GeneratePoints;

public sealed class GeneratePointsSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly SimpleStarmapSystem _starmapSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnRoundStartAttempt);
    }

    private void OnRoundStartAttempt(RoundStartAttemptEvent args)
    {
        if (args.Forced || args.Cancelled) return;
        if (!_configurationManager.GetCVar(CLVars.GenerateStarmapRoundstart)) return;
        var sectorMapId = _mapManager.CreateMap();
        var sectorUid = _mapManager.GetMapEntityId(sectorMapId);
        var starMapComponent = AddComp<StarMapComponent>(sectorUid);
        _starmapSystem.GenerateInitialSector(sectorUid, starMapComponent);
    }
}
