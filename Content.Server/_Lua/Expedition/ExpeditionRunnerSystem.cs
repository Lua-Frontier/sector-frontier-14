// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Lua.Expedition;
using Content.Shared._Lua.Stargate.PlanetQuest;
using Content.Shared._NF.CCVar;
using Content.Shared.Chat;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Expedition;

public sealed class ExpeditionRunnerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsoles = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly StationSystem _station = default!;
    private float _travelTime;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExpeditionMapComponent, MapInitEvent>(OnExpeditionMapInit);
        SubscribeLocalEvent<ExpeditionMapComponent, ComponentGetState>(OnExpeditionGetState);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnConsoleFTLAttempt);
        _travelTime = _cfg.GetCVar(NFCCVars.SalvageExpeditionTravelTime);
        Subs.CVar(_cfg, NFCCVars.SalvageExpeditionTravelTime, v => _travelTime = v);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ExpeditionMapComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Completed && TryComp<PlanetQuestComponent>(uid, out var quest) && quest.Completed)
            {
                comp.Completed = true;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("expedition-completed"));
            }
            var remaining = comp.EndTime - _timing.CurTime;
            var audioLength = _audio.GetAudioLength(comp.SelectedSong);
            if (comp.Stage < ExpeditionStage.FinalCountdown && remaining < TimeSpan.FromSeconds(45))
            {
                comp.Stage = ExpeditionStage.FinalCountdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("expedition-announcement-countdown-seconds", ("duration", TimeSpan.FromSeconds(45).Seconds)));
            }
            else if (comp.Stage < ExpeditionStage.MusicCountdown && remaining < audioLength)
            {
                comp.Stage = ExpeditionStage.MusicCountdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("expedition-announcement-countdown-minutes", ("duration", audioLength.Minutes)));
                var mapId = Comp<MapComponent>(uid).MapId;
                _audio.PlayGlobal(comp.SelectedSong, Filter.BroadcastMap(mapId), false);
            }
            else if (comp.Stage < ExpeditionStage.Countdown && remaining < TimeSpan.FromMinutes(5))
            {
                comp.Stage = ExpeditionStage.Countdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("expedition-announcement-countdown-minutes", ("duration", TimeSpan.FromMinutes(5).Minutes)));
            }
            else if (!comp.DepartureStarted && remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime) + TimeSpan.FromSeconds(0.5))
            {
                var ftlTime = (float) remaining.TotalSeconds;
                if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime)) ftlTime = MathF.Max(0, (float) remaining.TotalSeconds - 0.5f);
                ftlTime = MathF.Min(ftlTime, _shuttle.DefaultStartupTime);
                if (AutoFtlShuttlesHome(uid, comp, ftlTime)) comp.DepartureStarted = true;
            }
            if (remaining < TimeSpan.Zero) QueueDel(uid);
        }
    }

    public void Announce(EntityUid mapUid, string text)
    {
        var mapId = Comp<MapComponent>(mapUid).MapId;
        _chat.ChatMessageToManyFiltered(Filter.BroadcastMap(mapId), ChatChannel.Radio, text, text, _mapSystem.GetMapOrInvalid(mapId), false, true, null);
    }

    private void OnExpeditionMapInit(EntityUid uid, ExpeditionMapComponent component, MapInitEvent args)
    { component.SelectedSong = _audio.ResolveSound(component.Sound); }

    private void OnExpeditionGetState(EntityUid uid, ExpeditionMapComponent component, ref ComponentGetState args)
    {
        args.State = new ExpeditionMapComponentState
        {
            Stage = component.Stage,
            EndTime = component.EndTime,
        };
    }

    private void OnConsoleFTLAttempt(ref ConsoleFTLAttemptEvent ev)
    {
        if (!TryComp(ev.Uid, out TransformComponent? xform) || !TryComp<ExpeditionMapComponent>(xform.MapUid, out _))
        { return; }
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var mobXform))
        {
            if (mobXform.MapUid != xform.MapUid) continue;
            if (_mobState.IsDead(uid, mobState) || _mobState.IsCritical(uid, mobState)) continue;
            if (mobXform.GridUid != ev.Uid)
            {
                ev.Cancelled = true;
                ev.Reason = Loc.GetString("expedition-not-all-present");
                return;
            }
        }
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        if (!TryComp<ExpeditionMapComponent>(args.MapUid, out var component)) return;
        if (component.Stage != ExpeditionStage.Added) return;
        if (TryComp<ExpeditionDataComponent>(component.Station, out var data))
        {
            data.CanFinish = true;
            Dirty(component.Station, data);
            _shuttleConsoles.RefreshShuttleConsoles();
        }
        Announce(args.MapUid, Loc.GetString("expedition-announcement-countdown-minutes", ("duration", (component.EndTime - _timing.CurTime).Minutes)));
        component.Stage = ExpeditionStage.Running;
        Dirty(args.MapUid, component);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        if (ev.FromMapUid is not { } fromMap || !TryComp<ExpeditionMapComponent>(fromMap, out var expedition)) return;
        expedition.DepartureStarted = true;
        if (TryComp<ExpeditionDataComponent>(expedition.Station, out var data))
        {
            data.CanFinish = false;
            Dirty(expedition.Station, data);
            _shuttleConsoles.RefreshShuttleConsoles();
        }
        if (!HasPlayerOnGrid(ev.Entity))
        {
            WipeExpeditionAfterEmptyDeparture(fromMap, expedition, ev.Entity);
            return;
        }
        var shuttleQuery = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();
        while (shuttleQuery.MoveNext(out _, out var shuttleXform))
        { if (shuttleXform.MapUid == fromMap) return; }
        QueueDel(fromMap);
    }

    private bool HasPlayerOnGrid(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<ActorComponent, HumanoidAppearanceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (xform.GridUid != gridUid) continue;
            if (HasComp<GhostComponent>(uid)) continue;
            return true;
        }
        return false;
    }

    private void WipeExpeditionAfterEmptyDeparture(EntityUid mapUid, ExpeditionMapComponent comp, EntityUid emptyShuttle)
    {
        comp.DepartureStarted = true;
        comp.Completed = false;
        Dirty(mapUid, comp);
        Announce(mapUid, Loc.GetString("expedition-failed"));
        QueueDel(emptyShuttle);
        ForceGhostActorsOnMap(mapUid);
        QueueDel(mapUid);
    }

    private void ForceGhostActorsOnMap(EntityUid mapUid)
    {
        var toGhost = new List<(EntityUid MindId, MindComponent Mind)>();
        var query = EntityQueryEnumerator<ActorComponent, MindContainerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mindContainer, out var xform))
        {
            if (xform.MapUid != mapUid) continue;
            if (!_mind.TryGetMind(uid, out var mindId, out var mind, mindContainer)) continue;
            if (HasComp<GhostComponent>(uid)) continue;
            toGhost.Add((mindId, mind));
        }
        foreach (var (mindId, mind) in toGhost) _ghost.OnGhostAttempt(mindId, canReturnGlobal: false, viaCommand: false, forced: true, mind: mind);
    }

    private bool AutoFtlShuttlesHome(EntityUid mapUid, ExpeditionMapComponent comp, float ftlTime)
    {
        var started = false;
        var shuttleQuery = AllEntityQuery<ShuttleComponent, TransformComponent>();
        while (shuttleQuery.MoveNext(out var shuttleUid, out var shuttle, out var shuttleXform))
        {
            if (shuttleXform.MapUid != mapUid || HasComp<FTLComponent>(shuttleUid)) continue;
            if (_station.GetOwningStation(shuttleUid, shuttleXform) != comp.Station) continue;
            EntityCoordinates destination;
            if (TryComp<ExpeditionDataComponent>(comp.Station, out var stationData) && stationData.ReturnMapUid != null)
            { destination = new EntityCoordinates(stationData.ReturnMapUid.Value, stationData.ReturnWorldPosition); }
            else
            {
                var mapId = _gameTicker.DefaultMap;
                if (!_mapSystem.TryGetMap(mapId, out var returnMapUid)) continue;
                destination = new EntityCoordinates(returnMapUid.Value, FindFallbackReturnLocation(mapId));
            }
            _shuttle.FTLToCoordinates(shuttleUid, shuttle, destination, 0f, ftlTime, _travelTime);
            started = true;
        }
        return started;
    }

    private Vector2 FindFallbackReturnLocation(MapId mapId)
    {
        const int numRetries = 20;
        const float minDistance = 200f;
        const float minRange = 750f;
        const float maxRange = 3500f;
        var gridCoords = new List<Vector2>();
        var gridQuery = AllEntityQuery<MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out _, out _, out var xform))
        { if (xform.MapID == mapId) gridCoords.Add(_transform.GetWorldPosition(xform)); }
        var dropLocation = _random.NextVector2(minRange, maxRange);
        for (var i = 0; i < numRetries; i++)
        {
            var positionIsValid = true;
            foreach (var station in gridCoords)
            {
                if (Vector2.Distance(station, dropLocation) < minDistance)
                {
                    positionIsValid = false;
                    break;
                }
            }
            if (positionIsValid) break;
            dropLocation = _random.NextVector2(minRange, maxRange);
        }
        return dropLocation;
    }
}
