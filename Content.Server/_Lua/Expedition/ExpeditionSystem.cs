// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server._Lua.Stargate.Systems;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._Lua.Expedition;
using Content.Shared.Lua.CLVar;
using Content.Shared._NF.CCVar;
using Content.Shared._NF.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Expedition;

public sealed class ExpeditionSystem : EntitySystem
{
    private const int MissionLimit = 5;
    private const float ShuttleFTLMassThreshold = 50f;
    private const float ShuttleFTLRange = 150f;
    private static readonly TimeSpan ExpeditionConfirmTimeout = TimeSpan.FromMinutes(3);
    private static readonly SoundSpecifier ConfirmBeepSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private static readonly HashSet<string> ExpeditionStationProtos = new()
    {
        "StandardFrontierExpeditionVessel",
        "StandardFrontierSecurityExpeditionVessel",
    };
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsoles = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StargatePlanetGeneratorSystem _planetGen = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ExpeditionRunnerSystem _runner = default!;
    private readonly Queue<QueuedExpeditionRequest> _expeditionQueue = new();
    private readonly HashSet<EntityUid> _queuedStations = new();
    private PendingExpeditionRequest? _pendingExpedition;
    private TimeSpan _confirmBeepNext = TimeSpan.Zero;
    private float _cooldown;
    private float _failedCooldown;
    private float _travelTime;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExpeditionDataComponent, ComponentInit>(OnExpeditionDataInit);
        SubscribeLocalEvent<ExpeditionMapComponent, ComponentShutdown>(OnExpeditionMapShutdown);
        SubscribeLocalEvent<ExpeditionMapComponent, EntityTerminatingEvent>(OnExpeditionMapTerminating);
        SubscribeLocalEvent<StationPostInitEvent>(OnExpeditionStationPostInit);
        Subs.CVar(_cfg, CCVars.SalvageExpeditionCooldown, v => _cooldown = v);
        Subs.CVar(_cfg, NFCCVars.SalvageExpeditionFailedCooldown, v => _failedCooldown = v);
        Subs.CVar(_cfg, NFCCVars.SalvageExpeditionTravelTime, v => _travelTime = v);
        _cooldown = _cfg.GetCVar(CCVars.SalvageExpeditionCooldown);
        _failedCooldown = _cfg.GetCVar(NFCCVars.SalvageExpeditionFailedCooldown);
        _travelTime = _cfg.GetCVar(NFCCVars.SalvageExpeditionTravelTime);
        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<ClaimExpeditionMessage>(OnClaimMessage);
            subs.Event<ConfirmExpeditionMessage>(OnConfirmMessage);
            subs.Event<CancelExpeditionMessage>(OnCancelMessage);
            subs.Event<FinishExpeditionMessage>(OnFinishMessage);
        });
    }

    public override void Update(float frameTime)
    {
        var currentTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ExpeditionDataComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextOffer > currentTime || comp.Claimed || IsStationQueuedOrPending(uid)) continue;
            if (!TryComp<StationDataComponent>(uid, out var stationData) || !HasComp<FTLComponent>(_station.GetLargestGrid(stationData))) { comp.Cooldown = false; }
            RefreshOffers(uid, comp);
            RefreshStationConsoles(uid);
        }
        if (_pendingExpedition != null && Deleted(_pendingExpedition.Station))
        {
            _pendingExpedition = null;
            RefreshAllConsoles();
            TryStartPendingConfirm();
        }
        else if (_pendingExpedition != null && currentTime >= _pendingExpedition.Deadline)
        {
            _pendingExpedition = null;
            RefreshAllConsoles();
            TryStartPendingConfirm();
        }
        if (_pendingExpedition != null)
        {
            if (_confirmBeepNext <= currentTime)
            {
                PlayConfirmBeep(_pendingExpedition.Station);
                _confirmBeepNext = currentTime + TimeSpan.FromSeconds(1);
            }
        }
        else { _confirmBeepNext = TimeSpan.Zero; }
    }

    public ExpeditionConsoleState? GetExpeditionStateForConsole(EntityUid consoleUid, EntityUid? shuttleGridUid)
    {
        if (!TryResolveExpeditionStation(consoleUid, shuttleGridUid, out var station, out var data)) return null;
        TryEnsureOffers(station, data);
        return GetExpeditionState(station, shuttleGridUid, data);
    }

    public ExpeditionConsoleState GetExpeditionState(EntityUid station, EntityUid? shuttleGrid, ExpeditionDataComponent data)
    {
        var enabled = _cfg.GetCVar(CLVars.SalvageExpeditionEnabled);
        var (inCombat, massAllowed, currentMass, massLimit, blockReason) = EvaluateShuttleConstraints(shuttleGrid, enabled);
        List<ExpeditionOfferListing> missions;
        if (!enabled)
        { missions = new List<ExpeditionOfferListing>(); }
        else
        {
            missions = new List<ExpeditionOfferListing>(data.Missions.Count);
            foreach (var missionParams in data.Missions.Values.OrderBy(m => m.Index))
            {
                var offer = _planetGen.ResolveOfferFromSeed(missionParams.Seed);
                if (offer == null) continue;
                missions.Add(new ExpeditionOfferListing(missionParams.Index, missionParams.Seed, offer.PlanetName, offer.BiomeId, offer.AirDescription, offer.WeatherDescription, missionParams.Reward, missionParams.Duration, missionParams.PresetId, missionParams.QuestId));
            }
        }
        var isOurTurn = _pendingExpedition != null && _pendingExpedition.Station == station;
        var hasDeadline = isOurTurn;
        var isQueued = IsStationQueuedOrPending(station);
        var (queuePosition, queueTotal) = GetQueuePosition(station);
        var deadline = hasDeadline ? _pendingExpedition!.Deadline - _timing.CurTime : TimeSpan.Zero;
        if (deadline < TimeSpan.Zero) deadline = TimeSpan.Zero;
        var cooldown = data.Cooldown;
        if (shuttleGrid != null && HasComp<FTLComponent>(shuttleGrid.Value)) cooldown = true;
        return new ExpeditionConsoleState(
            data.NextOffer,
            data.Claimed || isQueued,
            cooldown,
            data.ActiveMission,
            missions,
            data.CanFinish,
            data.CooldownTime,
            GetActiveExpeditionCount(),
            isOurTurn,
            hasDeadline,
            deadline,
            isQueued,
            queuePosition,
            queueTotal,
            inCombat,
            massAllowed,
            currentMass,
            massLimit,
            blockReason,
            enabled,
            data.Generating,
            data.GenerationProgress,
            data.HasLandingCoords,
            data.LandingCoordsX,
            data.LandingCoordsY,
            data.LandingCoordCode);
    }

    public bool TryClaim(EntityUid console, EntityUid shuttleGrid, ClaimExpeditionMessage args)
    {
        if (!TryResolveExpeditionStation(console, shuttleGrid, out var station, out var data)) return false;
        if (data.Claimed) return false;
        if (!_cfg.GetCVar(CLVars.SalvageExpeditionEnabled))
        {
            PlayDeny(console);
            RefreshAllConsoles();
            return false;
        }
        var enabled = true;
        var (_, _, _, _, blockReason) = EvaluateShuttleConstraints(shuttleGrid, enabled);
        if (blockReason != null)
        {
            PlayDeny(console);
            _popup.PopupEntity(blockReason, console, PopupType.MediumCaution);
            RefreshAllConsoles();
            return false;
        }
        if (!data.Missions.TryGetValue(args.Index, out var missionParams)) return false;
        if (missionParams.Seed != args.Seed)
        {
            PlayDeny(console);
            RefreshStationConsoles(station);
            return false;
        }
        var activeExpeditionCount = GetActiveExpeditionCount();
        if (activeExpeditionCount >= _cfg.GetCVar(NFCCVars.SalvageExpeditionMaxActive) || _pendingExpedition != null)
        {
            if (!EnqueueExpedition(station, missionParams))
            {
                PlayDeny(console);
                RefreshAllConsoles();
                return false;
            }
            RefreshAllConsoles();
            TryStartPendingConfirm();
            return true;
        }
        if (!TryStartExpedition(console, shuttleGrid, station, data, missionParams)) return false;
        RefreshAllConsoles();
        return true;
    }

    public bool TryConfirm(EntityUid console, EntityUid shuttleGrid)
    {
        if (!TryResolveExpeditionStation(console, shuttleGrid, out var station, out var data) || _pendingExpedition == null || _pendingExpedition.Station != station)
        { return false; }
        if (data.Claimed) return false;
        if (!_cfg.GetCVar(CLVars.SalvageExpeditionEnabled))
        {
            PlayDeny(console);
            RefreshAllConsoles();
            return false;
        }
        if (!TryStartExpedition(console, shuttleGrid, station, data, _pendingExpedition.MissionParams)) return false;
        _pendingExpedition = null;
        RefreshAllConsoles();
        TryStartPendingConfirm();
        return true;
    }

    public bool TryCancel(EntityUid console, EntityUid shuttleGrid)
    {
        var station = ResolveStation(console, shuttleGrid);
        if (station == null || _pendingExpedition == null || _pendingExpedition.Station != station.Value) return false;
        _pendingExpedition = null;
        RefreshAllConsoles();
        TryStartPendingConfirm();
        return true;
    }

    public bool TryFinish(EntityUid console, EntityUid shuttleGrid)
    {
        if (!TryResolveExpeditionStation(console, shuttleGrid, out var station, out var data) || !data.CanFinish) return false;
        if (!TryComp(console, out TransformComponent? consoleXform))
        {
            PlayDeny(console);
            _popup.PopupEntity(Loc.GetString("expedition-shuttle-not-found"), console, PopupType.MediumCaution);
            RefreshStationConsoles(station);
            return false;
        }
        var mobQuery = EntityQueryEnumerator<MindContainerComponent, MobStateComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var uid, out var mindContainer, out _, out var mobXform))
        {
            if (mobXform.MapUid != consoleXform.MapUid) continue;
            if (!mindContainer.HasMind) continue;
            if (HasComp<ActiveNPCComponent>(uid)) continue;
            if (mobXform.GridUid != shuttleGrid)
            {
                PlayDeny(console);
                _popup.PopupEntity(Loc.GetString("expedition-not-everyone-aboard", ("target", uid)), console, PopupType.MediumCaution);
                RefreshStationConsoles(station);
                return false;
            }
        }
        var map = consoleXform.MapUid;
        if (map == null || !TryComp<ExpeditionMapComponent>(map, out var expedition)) return false;
        var ftlQuery = AllEntityQuery<FTLComponent, TransformComponent>();
        while (ftlQuery.MoveNext(out var ftl, out var ftlXform))
        {
            if (ftlXform.MapUid != map) continue;
            if (ftl.State == FTLState.Cooldown)
            {
                PlayDeny(console);
                _popup.PopupEntity(Loc.GetString("shuttle-ftl-recharge"), console, PopupType.MediumCaution);
                RefreshStationConsoles(station);
                return false;
            }
        }
        const int departTime = 20;
        var newEndTime = _timing.CurTime + TimeSpan.FromSeconds(departTime);
        if (expedition.EndTime <= newEndTime) return false;
        data.CanFinish = false;
        RefreshStationConsoles(station);
        expedition.Stage = ExpeditionStage.FinalCountdown;
        expedition.EndTime = newEndTime;
        Dirty(map.Value, expedition);
        _runner.Announce(map.Value, Loc.GetString("expedition-announcement-early-finish", ("departTime", departTime)));
        return true;
    }

    internal void FinishExpedition(EntityUid mapUid, EntityUid station, ExpeditionDataComponent data, ExpeditionMapComponent expeditionComp)
    {
        if (expeditionComp.Completed)
        {
            data.NextOffer = _timing.CurTime + TimeSpan.FromSeconds(_cooldown);
            data.CooldownTime = TimeSpan.FromSeconds(_cooldown);
            _runner.Announce(mapUid, Loc.GetString("expedition-completed"));
        }
        else
        {
            data.NextOffer = _timing.CurTime + TimeSpan.FromSeconds(_failedCooldown);
            data.CooldownTime = TimeSpan.FromSeconds(_failedCooldown);
            _runner.Announce(mapUid, Loc.GetString("expedition-failed"));
        }
        data.ActiveMission = 0;
        data.Cooldown = true;
        data.CanFinish = false;
        data.ReturnMapUid = null;
        data.ReturnWorldPosition = Vector2.Zero;
        ClearGenerationState(data);
        RefreshStationConsoles(station);
        RefreshAllConsoles();
        TryStartPendingConfirm();
    }

    private void OnExpeditionDataInit(EntityUid uid, ExpeditionDataComponent component, ComponentInit args)
    { TryEnsureOffers(uid, component); }

    private void OnExpeditionStationPostInit(ref StationPostInitEvent ev)
    {
        var protoId = MetaData(ev.Station).EntityPrototype?.ID;
        if (protoId == null || !ExpeditionStationProtos.Contains(protoId)) return;
        foreach (var grid in ev.Station.Comp.Grids) EnsureComp<ExpeditionShuttleComponent>(grid);
    }

    private void OnExpeditionMapShutdown(EntityUid uid, ExpeditionMapComponent component, ComponentShutdown args)
    {
        if (Deleted(component.Station) || !TryComp<ExpeditionDataComponent>(component.Station, out var data))
            return;

        FinishExpedition(uid, component.Station, data, component);
    }

    private void OnExpeditionMapTerminating(EntityUid uid, ExpeditionMapComponent component, EntityTerminatingEvent args)
    {
        ClearExpeditionCrewMarkers(uid);

        var ghosts = EntityQueryEnumerator<GhostComponent, TransformComponent>();
        var newCoords = new MapCoordinates(Vector2.Zero, _gameTicker.DefaultMap);
        while (ghosts.MoveNext(out var ghostUid, out _, out var xform))
        {
            if (xform.MapUid == uid)
                _transform.SetMapCoordinates(ghostUid, newCoords);
        }
    }

    private void ClearExpeditionCrewMarkers(EntityUid expeditionMap)
    {
        var toRemove = new List<EntityUid>();
        var query = EntityQueryEnumerator<ExpeditionCrewMemberComponent>();
        while (query.MoveNext(out var uid, out var crew))
        {
            if (crew.ExpeditionMap == expeditionMap)
                toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
            RemComp<ExpeditionCrewMemberComponent>(uid);
    }

    private void OnClaimMessage(EntityUid uid, ShuttleConsoleComponent component, ClaimExpeditionMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
            return;

        TryClaim(uid, xform.GridUid.Value, args);
    }

    private void OnConfirmMessage(EntityUid uid, ShuttleConsoleComponent component, ConfirmExpeditionMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
            return;

        TryConfirm(uid, xform.GridUid.Value);
    }

    private void OnCancelMessage(EntityUid uid, ShuttleConsoleComponent component, CancelExpeditionMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
            return;

        TryCancel(uid, xform.GridUid.Value);
    }

    private void OnFinishMessage(EntityUid uid, ShuttleConsoleComponent component, FinishExpeditionMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null)
            return;

        TryFinish(uid, xform.GridUid.Value);
    }

    private bool TryStartExpedition(
        EntityUid console,
        EntityUid shuttleGrid,
        EntityUid station,
        ExpeditionDataComponent data,
        ExpeditionMissionParams missionParams)
    {
        var enabled = _cfg.GetCVar(CLVars.SalvageExpeditionEnabled);
        var (_, massAllowed, _, _, blockReason) = EvaluateShuttleConstraints(shuttleGrid, enabled);

        if (!enabled || blockReason != null)
        {
            PlayDeny(console);
            if (blockReason != null)
                _popup.PopupEntity(blockReason, console, PopupType.MediumCaution);

            RefreshStationConsoles(station);
            return false;
        }

        if (!massAllowed)
        {
            PlayDeny(console);
            _popup.PopupEntity(Loc.GetString("expedition-mass-block"), console, PopupType.MediumCaution);
            RefreshStationConsoles(station);
            return false;
        }



        if (HasComp<FTLComponent>(shuttleGrid))
        {
            PlayDeny(console);
            _popup.PopupEntity(Loc.GetString("shuttle-ftl-recharge"), console, PopupType.MediumCaution);
            RefreshStationConsoles(station);
            return false;
        }

        var consoleXform = Transform(console);
        if (consoleXform.MapUid != null)
        {
            data.ReturnMapUid = consoleXform.MapUid.Value;
            data.ReturnWorldPosition = _transform.GetWorldPosition(consoleXform);
        }

        data.ActiveMission = missionParams.Index;
        data.NextOffer = _timing.CurTime + missionParams.Duration + TimeSpan.FromSeconds(1);
        data.CooldownTime = missionParams.Duration + TimeSpan.FromSeconds(1);
        data.Generating = true;
        data.GenerationProgress = 0.05f;
        data.HasLandingCoords = false;
        data.LandingCoordsX = 0;
        data.LandingCoordsY = 0;
        data.LandingCoordCode = string.Empty;
        RefreshStationConsoles(station);

        _ = RunExpeditionAsync(console, shuttleGrid, station, data, missionParams);
        return true;
    }

    private void SetGenerationProgress(EntityUid station, ExpeditionDataComponent data, float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        if (data.Generating && progress < 0.999f && Math.Abs(progress - data.GenerationProgress) < 0.02f) return;
        data.Generating = true;
        data.GenerationProgress = progress;
        RefreshStationConsoles(station);
    }

    private void ClearGenerationState(ExpeditionDataComponent data)
    {
        data.Generating = false;
        data.GenerationProgress = 0f;
        data.HasLandingCoords = false;
        data.LandingCoordsX = 0;
        data.LandingCoordsY = 0;
        data.LandingCoordCode = string.Empty;
    }

    private async Task YieldGenerationTick()
    {
        await Task.Yield();
    }
    private static string FormatLandingCoordCode(int seed, string planetName)
    {
        const string alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        var rng = new System.Random(seed);
        var buf = new char[8];
        for (var i = 0; i < buf.Length; i++) buf[i] = alphabet[rng.Next(alphabet.Length)];
        if (string.IsNullOrWhiteSpace(planetName)) planetName = "UNKNOWN";
        return $"{new string(buf)}-{planetName}";
    }

    private async Task RunExpeditionAsync(
        EntityUid console,
        EntityUid shuttleGrid,
        EntityUid station,
        ExpeditionDataComponent data,
        ExpeditionMissionParams missionParams)
    {
        try
        {
            void ReportProgress(float progress)
            {
                if (Deleted(station) || !TryComp<ExpeditionDataComponent>(station, out var live))
                    return;

                if (live.ActiveMission != missionParams.Index)
                    return;

                SetGenerationProgress(station, live, progress);
            }

            ReportProgress(0.08f);

            var mapUid = await _planetGen.CreateExpeditionPlanetAsync(
                missionParams.Seed,
                missionParams.PresetId,
                missionParams.QuestId,
                missionParams.Reward,
                progress: ReportProgress);

            if (mapUid == null || Deleted(station) || !TryComp<ExpeditionDataComponent>(station, out var currentData))
                return;

            if (currentData.ActiveMission != missionParams.Index)
                return;

            if (Deleted(shuttleGrid) || !TryComp<ShuttleComponent>(shuttleGrid, out var shuttle))
            {
                currentData.ActiveMission = 0;
                currentData.Cooldown = false;
                ClearGenerationState(currentData);
                RefreshStationConsoles(station);
                return;
            }

            ReportProgress(0.92f);
            await YieldGenerationTick();

            var expedition = EnsureComp<ExpeditionMapComponent>(mapUid.Value);
            expedition.Station = station;
            expedition.EndTime = _timing.CurTime + missionParams.Duration;
            expedition.Seed = missionParams.Seed;
            MarkDepartingCrew(shuttleGrid, mapUid.Value);
            Dirty(mapUid.Value, expedition);

            var landing = Vector2.Zero;
            if (TryComp<ExpeditionPlanetComponent>(mapUid, out var planet))
                landing = planet.LandingOrigin;

            currentData.LandingCoordsX = (int)MathF.Round(landing.X);
            currentData.LandingCoordsY = (int)MathF.Round(landing.Y);
            currentData.LandingCoordCode = FormatLandingCoordCode(
                missionParams.Seed,
                Name(mapUid.Value));
            currentData.HasLandingCoords = true;
            currentData.GenerationProgress = 1f;
            currentData.Generating = false;
            RefreshStationConsoles(station);

            await YieldGenerationTick();

            _shuttle.FTLToCoordinates(
                shuttleGrid,
                shuttle,
                new EntityCoordinates(mapUid.Value, landing),
                0f,
                5.5f,
                _travelTime);
        }
        catch (Exception e)
        {
            Log.Error($"Expedition planet generation failed: {e}");
            if (TryComp<ExpeditionDataComponent>(station, out var currentData)
                && currentData.ActiveMission == missionParams.Index)
            {
                currentData.ActiveMission = 0;
                currentData.Cooldown = false;
                currentData.ReturnMapUid = null;
                currentData.ReturnWorldPosition = Vector2.Zero;
                ClearGenerationState(currentData);
                RefreshStationConsoles(station);
            }
        }
    }

    private void MarkDepartingCrew(EntityUid shuttleGrid, EntityUid expeditionMap)
    {
        if (!TryComp(shuttleGrid, out TransformComponent? shuttleXform))
            return;

        var query = EntityQueryEnumerator<ActorComponent, HumanoidAppearanceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (xform.MapUid != shuttleXform.MapUid) continue;
            if (xform.GridUid != shuttleGrid) continue;
            if (HasComp<GhostComponent>(uid)) continue;
            if (HasComp<ActiveNPCComponent>(uid)) continue;

            var crew = EnsureComp<ExpeditionCrewMemberComponent>(uid);
            crew.ExpeditionMap = expeditionMap;
        }
    }

    private bool TryResolveExpeditionStation(
        EntityUid console,
        EntityUid? shuttleGrid,
        out EntityUid station,
        out ExpeditionDataComponent data)
    {
        station = EntityUid.Invalid;
        data = null!;

        if (!TryResolveShuttleGrid(console, shuttleGrid, out var grid)
            || !HasComp<ExpeditionShuttleComponent>(grid))
        {
            return false;
        }

        var resolved = ResolveStation(console, shuttleGrid);
        if (resolved == null)
            return false;

        station = resolved.Value;

        if (!TryComp<ExpeditionDataComponent>(station, out var expeditionData))
            expeditionData = AddComp<ExpeditionDataComponent>(station);
        else
            TryEnsureOffers(station, expeditionData);

        data = expeditionData;
        return true;
    }

    private bool TryResolveShuttleGrid(EntityUid console, EntityUid? shuttleGrid, out EntityUid grid)
    {
        if (shuttleGrid != null && IsValidEntity(shuttleGrid.Value))
        {
            grid = shuttleGrid.Value;
            return true;
        }

        var consoleGrid = Transform(console).GridUid;
        if (consoleGrid != null && IsValidEntity(consoleGrid.Value))
        {
            grid = consoleGrid.Value;
            return true;
        }

        grid = EntityUid.Invalid;
        return false;
    }

    private void TryEnsureOffers(EntityUid station, ExpeditionDataComponent data)
    {
        if (data.Claimed || IsStationQueuedOrPending(station))
            return;

        if (data.Missions.Count > 0)
        {
            EnsureOfferSchedule(data);
            return;
        }

        if (data.NextOffer > _timing.CurTime)
        {
            data.NextOffer = _timing.CurTime;
        }

        RefreshOffers(station, data);
    }

    private void RefreshOffers(EntityUid station, ExpeditionDataComponent data)
    {
        GenerateMissions(data);

        if (data.Missions.Count == 0)
            return;

        data.CooldownTime = TimeSpan.FromSeconds(_cooldown);
        data.NextOffer = _timing.CurTime + data.CooldownTime;
    }

    private void EnsureOfferSchedule(ExpeditionDataComponent data)
    {
        if (data.CooldownTime > TimeSpan.Zero)
            return;

        data.CooldownTime = data.NextOffer > _timing.CurTime
            ? data.NextOffer - _timing.CurTime
            : TimeSpan.FromSeconds(_cooldown);

        if (data.NextOffer <= _timing.CurTime)
            data.NextOffer = _timing.CurTime + data.CooldownTime;
    }

    private void GenerateMissions(ExpeditionDataComponent component)
    {
        component.Missions.Clear();
        var duration = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.SalvageExpeditionDuration));

        var attempts = 0;
        while (component.Missions.Count < MissionLimit && attempts < MissionLimit * 8)
        {
            attempts++;
            var seed = _random.Next();
            var offer = _planetGen.ResolveOfferFromSeed(seed);
            if (offer == null)
                continue;

            var mission = new ExpeditionMissionParams
            {
                Index = component.NextIndex,
                Seed = seed,
                PresetId = offer.PresetId,
                QuestId = offer.QuestId,
                Reward = offer.Reward,
                Duration = duration,
            };

            component.Missions[component.NextIndex++] = mission;
        }
    }

    private (bool InCombat, bool MassAllowed, float CurrentMass, float MassLimit, string? BlockReason)
        EvaluateShuttleConstraints(EntityUid? shuttleGrid, bool enabled)
    {
        var massLimit = _cfg.GetCVar(CLVars.ExpeditionMassLimit);
        if (massLimit <= 0)
            massLimit = PlanetMassLimits.ShuttleMassLimit;

        var currentMass = 0f;
        if (shuttleGrid != null && TryComp<PhysicsComponent>(shuttleGrid, out var body))
            currentMass = body.Mass;

        var massAllowed = currentMass <= massLimit;
        var inCombat = false;

        if (shuttleGrid != null && TryComp<ShuttleFTLComponent>(shuttleGrid, out var shuttleFtl))
            inCombat = shuttleFtl.CombatUntil > _timing.CurTime;

        string? blockReason = null;
        if (!enabled)
            blockReason = Loc.GetString("expedition-disabled");
        else if (inCombat)
            blockReason = Loc.GetString("expedition-combat-block");
        else if (!massAllowed)
            blockReason = Loc.GetString("expedition-mass-block", ("mass", currentMass), ("limit", massLimit));

        return (inCombat, massAllowed, currentMass, massLimit, blockReason);
    }

    private EntityUid? ResolveStation(EntityUid console, EntityUid? shuttleGrid)
    {
        EntityUid? station = null;

        if (console.IsValid() && !Deleted(console))
            station = _station.GetOwningStation(console);

        if ((station == null || !IsValidEntity(station.Value))
            && shuttleGrid != null
            && IsValidEntity(shuttleGrid.Value))
        {
            station = _station.GetOwningStation(shuttleGrid.Value);
        }

        if (station != null && IsValidEntity(station.Value))
            return station.Value;

        var xform = Transform(console);
        if (xform.MapUid != null
            && TryComp<ExpeditionMapComponent>(xform.MapUid.Value, out var expedition)
            && IsValidEntity(expedition.Station))
        {
            return expedition.Station;
        }

        return null;
    }

    private bool IsValidEntity(EntityUid uid) => uid.IsValid() && !Deleted(uid);

    private int GetActiveExpeditionCount()
    {
        var count = 0;
        var query = AllEntityQuery<ExpeditionDataComponent>();
        while (query.MoveNext(out _, out var data))
        {
            if (data.Claimed)
                count++;
        }

        return count;
    }

    private int GetQueueCount() => _expeditionQueue.Count + (_pendingExpedition != null ? 1 : 0);

    private bool EnqueueExpedition(EntityUid station, ExpeditionMissionParams missionParams)
    {
        if (IsStationQueuedOrPending(station))
            return false;

        _expeditionQueue.Enqueue(new QueuedExpeditionRequest(station, missionParams));
        _queuedStations.Add(station);
        return true;
    }

    private void TryStartPendingConfirm()
    {
        if (_pendingExpedition != null)
            return;

        if (GetActiveExpeditionCount() >= _cfg.GetCVar(NFCCVars.SalvageExpeditionMaxActive))
            return;

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

            _pendingExpedition = new PendingExpeditionRequest(
                request.Station,
                request.MissionParams,
                _timing.CurTime + ExpeditionConfirmTimeout);
            NotifyQueueReady(request.Station);
            RefreshAllConsoles();
            return;
        }

        if (queueChanged)
            RefreshAllConsoles();
    }

    private void NotifyQueueReady(EntityUid station)
    {
        var query = AllEntityQuery<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (ResolveStation(uid, xform.GridUid) != station)
                continue;

            _popup.PopupEntity(Loc.GetString("expedition-queue-ready"), uid, PopupType.Medium);
        }
    }

    private void RefreshStationConsoles(EntityUid station)
    {
        _shuttleConsoles.RefreshShuttleConsoles();
    }

    private void RefreshAllConsoles()
    {
        _shuttleConsoles.RefreshShuttleConsoles();
    }

    private void PlayConfirmBeep(EntityUid station)
    {
        var query = AllEntityQuery<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (ResolveStation(uid, xform.GridUid) != station)
                continue;

            _audio.PlayPvs(_audio.ResolveSound(ConfirmBeepSound), uid);
        }
    }

    private void PlayDeny(EntityUid console)
    {
        _audio.PlayPvs(_audio.ResolveSound(DenySound), console);
    }

    private bool IsStationQueuedOrPending(EntityUid station) =>
        (_pendingExpedition != null && _pendingExpedition.Station == station) || _queuedStations.Contains(station);

    private (int Position, int Total) GetQueuePosition(EntityUid station)
    {
        var total = GetQueueCount();
        if (total == 0)
            return (0, 0);

        if (_pendingExpedition != null && _pendingExpedition.Station == station)
            return (1, total);

        var position = 1 + (_pendingExpedition != null ? 1 : 0);
        foreach (var request in _expeditionQueue)
        {
            if (request.Station == station)
                return (position, total);

            position++;
        }

        return (0, total);
    }
}

internal sealed record QueuedExpeditionRequest(EntityUid Station, ExpeditionMissionParams MissionParams);

internal sealed record PendingExpeditionRequest(
    EntityUid Station,
    ExpeditionMissionParams MissionParams,
    TimeSpan Deadline);
