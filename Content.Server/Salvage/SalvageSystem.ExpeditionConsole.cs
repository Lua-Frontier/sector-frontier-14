using Content.Server._NF.Salvage; // Frontier
using Content.Server.Salvage.Expeditions; // Frontier
using Content.Server.Station.Components; // Frontier
using Content.Shared._NF.CCVar; // Frontier
using Content.Shared.Dataset;
using Content.Shared.Mind.Components; // Frontier
using Content.Shared.Mobs.Components; // Frontier
using Content.Shared.NPC; // Frontier
using Content.Shared.NPC.Components; // Frontier
using Content.Shared.Popups; // Frontier
using Content.Shared.Procedural;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Map.Components; // Frontier
using Robust.Shared.Physics; // Frontier
using Robust.Shared.Physics.Components; // Frontier
using Robust.Shared.Prototypes;

namespace Content.Server.Salvage;

public sealed partial class SalvageSystem
{
    public static readonly EntProtoId CoordinatesDisk = "CoordinatesDisk";
    public static readonly ProtoId<LocalizedDatasetPrototype> PlanetNames = "NamesBorer";

    [Dependency] private readonly SharedPopupSystem _popupSystem = default!; // Frontier
    [Dependency] private readonly SalvageSystem _salvage = default!; // Frontier

    private const float ShuttleFTLMassThreshold = 50f; // Frontier
    private const float ShuttleFTLRange = 150f; // Frontier

    private void OnSalvageClaimMessage(EntityUid uid, SalvageExpeditionConsoleComponent component, ClaimSalvageMessage args)
    {
        var station = GetConsoleStation(uid);
        if (station == null) return;

        if (!TryComp<SalvageExpeditionDataComponent>(station.Value, out var data) || data.Claimed)
            return;

        if (!data.Missions.TryGetValue(args.Index, out var missionparams))
            return;

        if (missionparams.Seed != args.Seed)
        {
            PlayDenySound((uid, component));
            UpdateConsoles((station.Value, data));
            return;
        }

        // Lua: prevent expeditions.
        var activeExpeditionCount = GetActiveExpeditionCount();
        if (activeExpeditionCount >= _cfgManager.GetCVar(NFCCVars.SalvageExpeditionMaxActive) || _pendingExpedition != null)
        {
            if (!EnqueueExpedition(station.Value, missionparams))
            {
                PlayDenySound((uid, component));
                UpdateAllConsoles();
                return;
            }
            UpdateAllConsoles();
            TryStartPendingConfirm();
            return;
        }
        // End Lua

        // var cdUid = Spawn(CoordinatesDisk, Transform(uid).Coordinates); // Frontier: no disk-based FTL
        // SpawnMission(missionparams, station.Value, cdUid); // Frontier: no disk-based FTL

        if (!TryStartExpedition(uid, component, station.Value, data, missionparams))
            return;

        UpdateAllConsoles();
    }

    // Frontier: early expedition end
    private void OnSalvageFinishMessage(EntityUid entity, SalvageExpeditionConsoleComponent component, FinishSalvageMessage e)
    {
        var station = GetConsoleStation(entity);
        if (station == null || !TryComp<SalvageExpeditionDataComponent>(station.Value, out var data) || !data.CanFinish)
            return;

        // Based on SalvageSystem.Runner:OnConsoleFTLAttempt
        if (!TryComp(entity, out TransformComponent? xform)) // Get the console's grid (if you move it, rip you)
        {
            PlayDenySound((entity, component));
            _popupSystem.PopupEntity(Loc.GetString("salvage-expedition-shuttle-not-found"), entity, PopupType.MediumCaution);
            UpdateConsoles((station.Value, data));
            return;
        }

        // Frontier: check if any player characters or friendly ghost roles are outside
        var query = EntityQueryEnumerator<MindContainerComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mindContainer, out var _, out var mobXform))
        {
            if (mobXform.MapUid != xform.MapUid)
                continue;

            // Not player controlled (ghosted)
            if (!mindContainer.HasMind)
                continue;

            // NPC, definitely not a person
            if (HasComp<ActiveNPCComponent>(uid) || HasComp<NFSalvageMobRestrictionsComponent>(uid))
                continue;

            // Hostile ghost role, continue
            if (TryComp(uid, out NpcFactionMemberComponent? npcFaction))
            {
                var hostileFactions = npcFaction.HostileFactions;
                if (hostileFactions.Contains("NanoTrasen")) // TODO: move away from hardcoded faction
                    continue;
            }

            // Okay they're on salvage, so are they on the shuttle.
            if (mobXform.GridUid != xform.GridUid)
            {
                PlayDenySound((entity, component));
                _popupSystem.PopupEntity(Loc.GetString("salvage-expedition-not-everyone-aboard", ("target", uid)), entity, PopupType.MediumCaution);
                UpdateConsoles((station.Value, data));
                return;
            }
        }
        // End SalvageSystem.Runner:OnConsoleFTLAttempt

        var map = Transform(entity).MapUid;

        if (!TryComp<SalvageExpeditionComponent>(map, out var expedition))
            return;

        // Lua start
        var ftlQuery = AllEntityQuery<FTLComponent, TransformComponent>();
        while (ftlQuery.MoveNext(out var ftl, out var ftlXform))
        {
            if (ftlXform.MapUid != map)
                continue;

            if (ftl.State == FTLState.Cooldown)
            {
                PlayDenySound((entity, component));
                _popupSystem.PopupEntity(Loc.GetString("shuttle-ftl-recharge"), entity, PopupType.MediumCaution);
                UpdateConsoles((station.Value, data));
                return;
            }
        }
        // Lua end

        const int departTime = 20;
        var newEndTime = _timing.CurTime + TimeSpan.FromSeconds(departTime);

        if (expedition.EndTime <= newEndTime)
            return;

        data.CanFinish = false; // Lua
        UpdateConsoles((station.Value, data));

        expedition.Stage = ExpeditionStage.FinalCountdown;
        expedition.EndTime = newEndTime;
        Dirty(map.Value, expedition);

        Announce(map.Value, Loc.GetString("salvage-expedition-announcement-early-finish", ("departTime", departTime)));
    }
    // End Frontier: early expedition end

    private void OnSalvageConsoleInit(Entity<SalvageExpeditionConsoleComponent> console, ref ComponentInit args)
    {
        UpdateConsole(console);
    }

    private void OnSalvageConsoleParent(Entity<SalvageExpeditionConsoleComponent> console, ref EntParentChangedMessage args)
    {
        UpdateConsole(console);
    }

    private void UpdateConsoles(Entity<SalvageExpeditionDataComponent> component)
    {
        var state = GetState(component.Owner, component.Comp);

        var query = AllEntityQuery<SalvageExpeditionConsoleComponent, UserInterfaceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var uiComp, out var xform))
        {
            var station = GetConsoleStation(uid, xform);

            if (station != component.Owner)
                continue;

            _ui.SetUiState((uid, uiComp), SalvageConsoleUiKey.Expedition, state);
        }
    }

    private void UpdateConsole(Entity<SalvageExpeditionConsoleComponent> component)
    {
        var station = GetConsoleStation(component.Owner);
        SalvageExpeditionConsoleState state;

        if (station != null && TryComp<SalvageExpeditionDataComponent>(station.Value, out var dataComponent))
        {
            state = GetState(station.Value, dataComponent);
        }
        else
        {
            state = new SalvageExpeditionConsoleState(TimeSpan.Zero, false, true, 0, new List<SalvageMissionListing>(), false, TimeSpan.FromSeconds(1), GetActiveExpeditionCount(), false, false, TimeSpan.Zero, false, 0, GetQueueCount()); // Lua \o/
        }

        // Frontier: if we have a lingering FTL component, we cannot start a new mission
        if (station == null ||
                !TryComp<StationDataComponent>(station.Value, out var stationData) ||
                _station.GetLargestGrid(stationData) is not { Valid: true } grid ||
                HasComp<FTLComponent>(grid))
        {
            state.Cooldown = true; //Hack: disable buttons
        }
        // End Frontier

        _ui.SetUiState(component.Owner, SalvageConsoleUiKey.Expedition, state);
    }

    // Frontier: deny sound
    private void PlayDenySound(Entity<SalvageExpeditionConsoleComponent> ent)
    {
        _audio.PlayPvs(_audio.ResolveSound(ent.Comp.ErrorSound), ent);
    }
    // End Frontier

    // Lua: expedition queue
    private EntityUid? GetConsoleStation(EntityUid uid, TransformComponent? xform = null)
    {
        xform ??= Transform(uid);
        var station = _station.GetOwningStation(uid, xform);
        if (station != null) return station;
        if (xform.MapUid != null && TryComp<SalvageExpeditionComponent>(xform.MapUid.Value, out var expedition)) return expedition.Station;
        return null;
    }

    private int GetActiveExpeditionCount()
    {
        var activeExpeditionCount = 0;
        var expeditionQuery = AllEntityQuery<SalvageExpeditionDataComponent>();
        while (expeditionQuery.MoveNext(out var expeditionData))
        { if (expeditionData.Claimed) activeExpeditionCount++; }
        return activeExpeditionCount;
    }

    private int GetQueueCount()
    { return _expeditionQueue.Count + (_pendingExpedition != null ? 1 : 0); }
    private bool EnqueueExpedition(EntityUid station, SalvageMissionParams missionParams)
    {
        if (IsStationQueuedOrPending(station)) return false;
        _expeditionQueue.Enqueue(new QueuedExpeditionRequest(station, missionParams));
        _queuedStations.Add(station);
        return true;
    }

    private void TryStartPendingConfirm()
    {
        if (_pendingExpedition != null) return;
        if (GetActiveExpeditionCount() >= _cfgManager.GetCVar(NFCCVars.SalvageExpeditionMaxActive)) return;
        var queueChanged = false;
        while (_expeditionQueue.Count > 0)
        {
            var request = _expeditionQueue.Dequeue();
            _queuedStations.Remove(request.Station);
            if (Deleted(request.Station))
            {
                queueChanged = true;
                continue;
            }
            _pendingExpedition = new PendingExpeditionRequest(request.Station, request.MissionParams, _timing.CurTime + ExpeditionConfirmTimeout);
            NotifyQueueReady(request.Station);
            UpdateAllConsoles();
            return;
        }
        if (queueChanged) UpdateAllConsoles();
    }
    private void NotifyQueueReady(EntityUid station)
    {
        var query = AllEntityQuery<SalvageExpeditionConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var consoleComp, out var xform))
        {
            var consoleStation = GetConsoleStation(uid, xform);
            if (consoleStation != station) continue;
            _popupSystem.PopupEntity(Loc.GetString("salvage-expedition-queue-ready"), uid, PopupType.Medium);
        }
    }

    private void UpdateAllConsoles()
    {
        var query = AllEntityQuery<SalvageExpeditionConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        { UpdateConsole((uid, comp)); }
    }
    private void PlayConfirmBeep(EntityUid station)
    {
        var query = AllEntityQuery<SalvageExpeditionConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var consoleStation = GetConsoleStation(uid, xform);
            if (consoleStation != station) continue;
            _audio.PlayPvs(_audio.ResolveSound(ConfirmBeepSound), uid);
        }
    }

    private bool IsStationQueuedOrPending(EntityUid station)
    { return (_pendingExpedition != null && _pendingExpedition.Station == station) || _queuedStations.Contains(station); }
    private (int Position, int Total) GetQueuePosition(EntityUid station)
    {
        var total = GetQueueCount();
        if (total == 0) return (0, 0);
        if (_pendingExpedition != null && _pendingExpedition.Station == station) return (1, total);
        var position = 1 + (_pendingExpedition != null ? 1 : 0);
        foreach (var request in _expeditionQueue)
        {
            if (request.Station == station) return (position, total);
            position++;
        }
        return (0, total);
    }

    private bool TryStartExpedition(EntityUid uid, SalvageExpeditionConsoleComponent component, EntityUid station, SalvageExpeditionDataComponent data, SalvageMissionParams missionparams)
    {
        if (_salvage.ProximityCheck && !component.Debug)
        {
            if (!TryComp<StationDataComponent>(station, out var stationData) || _station.GetLargestGrid(stationData) is not { Valid: true } ourGrid || !TryComp<MapGridComponent>(ourGrid, out var gridComp))
            {
                PlayDenySound((uid, component));
                _popupSystem.PopupEntity(Loc.GetString("shuttle-ftl-invalid"), uid, PopupType.MediumCaution);
                UpdateConsoles((station, data));
                return false;
            }
            if (HasComp<FTLComponent>(ourGrid))
            {
                PlayDenySound((uid, component));
                _popupSystem.PopupEntity(Loc.GetString("shuttle-ftl-recharge"), uid, PopupType.MediumCaution);
                UpdateConsoles((station, data));
                return false;
            }
            var xform = Transform(ourGrid);
            var bounds = _transform.GetWorldMatrix(ourGrid).TransformBox(gridComp.LocalAABB).Enlarged(ShuttleFTLRange);
            var bodyQuery = GetEntityQuery<PhysicsComponent>();
            var otherGrids = new List<Entity<MapGridComponent>>();
            _mapManager.FindGridsIntersecting(xform.MapID, bounds, ref otherGrids);
            var dockedShuttles = new HashSet<EntityUid>();
            _shuttle.GetAllDockedShuttlesIgnoringFTLLock(ourGrid, dockedShuttles);
            foreach (var otherGrid in otherGrids)
            {
                if (ourGrid == otherGrid.Owner || !bodyQuery.TryGetComponent(otherGrid.Owner, out var body) || body.Mass < ShuttleFTLMassThreshold && body.BodyType == BodyType.Dynamic)
                { continue; }
                if (dockedShuttles.Contains(otherGrid.Owner)) continue;
                PlayDenySound((uid, component));
                _popupSystem.PopupEntity(Loc.GetString("shuttle-ftl-proximity"), uid, PopupType.MediumCaution);
                UpdateConsoles((station, data));
                return false;
            }
        }

        var consoleXform = Transform(uid);
        if (consoleXform.MapUid != null)
        {
            data.ReturnMapUid = consoleXform.MapUid.Value;
            data.ReturnWorldPosition = _transform.GetWorldPosition(consoleXform);
        }

        data.ActiveMission = missionparams.Index;
        SpawnMission(missionparams, station, null);

        var mission = GetMission(missionparams.MissionType, _prototypeManager.Index<SalvageDifficultyPrototype>(missionparams.Difficulty), missionparams.Seed); // Frontier: add MissionType
        // Frontier - TODO: move this to progression for secondary window timer
        data.NextOffer = _timing.CurTime + mission.Duration + TimeSpan.FromSeconds(1);
        data.CooldownTime = mission.Duration + TimeSpan.FromSeconds(1); // Frontier
        UpdateConsoles((station, data));
        return true;
    }

    private void OnExpeditionConfirmMessage(EntityUid uid, SalvageExpeditionConsoleComponent component, ExpeditionConfirmMessage args)
    {
        var station = GetConsoleStation(uid);
        if (station == null || _pendingExpedition == null || _pendingExpedition.Station != station.Value)
            return;
        if (!TryComp<SalvageExpeditionDataComponent>(station.Value, out var data) || data.Claimed)
            return;
        if (!TryStartExpedition(uid, component, station.Value, data, _pendingExpedition.MissionParams))
            return;
        _pendingExpedition = null;
        UpdateAllConsoles();
        TryStartPendingConfirm();
    }
    private void OnExpeditionCancelMessage(EntityUid uid, SalvageExpeditionConsoleComponent component, ExpeditionCancelMessage args)
    {
        var station = GetConsoleStation(uid);
        if (station == null || _pendingExpedition == null || _pendingExpedition.Station != station.Value)
            return;

        _pendingExpedition = null;
        UpdateAllConsoles();
        TryStartPendingConfirm();
    }
    // Lua: expedition queue
}
