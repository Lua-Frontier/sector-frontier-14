using Content.Server._Lua.Sectors;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Radio.EntitySystems; // Frontier
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server.StationEvents.Events;

/// <summary>
///     An abstract entity system inherited by all station events for their behavior.
/// </summary>
public abstract class StationEventSystem<T> : GameRuleSystem<T> where T : IComponent
{
    [Dependency] protected readonly IAdminLogManager AdminLogManager = default!;
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] protected readonly ChatSystem ChatSystem = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly StationSystem StationSystem = default!;
    [Dependency] protected readonly RadioSystem RadioSystem = default!; // Frontier
    [Dependency] protected readonly MapSystem MapSystem = default!; // Frontier
    [Dependency] private readonly SectorIdleFreezeSystem _sectorIdleFreeze = default!;
    [Dependency] private readonly SectorSystem _sectors = default!;

    protected ISawmill Sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        Sawmill = Logger.GetSawmill("stationevents");
    }

    protected virtual MapId GetRelevantMapId()
    {
        return _sectors.TryGetHubMapId(out var hub) ? hub : MapId.Nullspace;
    }

    private bool TryGetRelevantMap(out MapId mapId, out EntityUid mapUid)
    {
        mapId = GetRelevantMapId();
        mapUid = EntityUid.Invalid;
        if (mapId == MapId.Nullspace || !MapSystem.MapExists(mapId))
            return false;
        mapUid = MapSystem.GetMap(mapId);
        return true;
    }

    /// <inheritdoc/>
    protected override void Added(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        AdminLogManager.Add(LogType.EventAnnounced, $"Event added / announced: {ToPrettyString(uid)}");

        if (!TryGetRelevantMap(out var mapId, out _))
            return;

        if (stationEvent.AnnouncementScope == StationEventAnnouncementScope.Station)
        {
            _sectorIdleFreeze.PinForRule(uid, mapId);
            return;
        }

        DispatchEventAnnouncement(uid, stationEvent, stationEvent.StartAnnouncement, stationEvent.StartAnnouncementColor, stationEvent.StartAudio);

        if (stationEvent.StartRadioAnnouncement != null)
        {
            if (TryGetRelevantMap(out _, out var mapUid))
            {
                var mapName = Name(mapUid);
                var message = Loc.GetString(stationEvent.StartRadioAnnouncement, ("mapdestination", mapName));
                RadioSystem.SendRadioMessage(uid, message, stationEvent.StartRadioAnnouncementChannel, mapUid, escapeMarkup: false);
            }
        }

        _sectorIdleFreeze.PinForRule(uid, mapId);
    }

    /// <inheritdoc/>
    protected override void Started(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        AdminLogManager.Add(LogType.EventStarted, LogImpact.High, $"Event started: {ToPrettyString(uid)}");

        if (stationEvent.Duration != null)
        {
            var duration = stationEvent.MaxDuration == null
                ? stationEvent.Duration
                : TimeSpan.FromSeconds(RobustRandom.NextDouble(stationEvent.Duration.Value.TotalSeconds,
                    stationEvent.MaxDuration.Value.TotalSeconds));
            stationEvent.EndTime = Timing.CurTime + duration;
        }
    }

    /// <inheritdoc/>
    protected override void Ended(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        _sectorIdleFreeze.UnpinForRule(uid);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        AdminLogManager.Add(LogType.EventStopped, $"Event ended: {ToPrettyString(uid)}");

        if (!TryGetRelevantMap(out _, out var mapUid))
            return;

        DispatchEventAnnouncement(uid, stationEvent, stationEvent.EndAnnouncement, stationEvent.EndAnnouncementColor, stationEvent.EndAudio);

        // Frontier: radio announcements
        if (stationEvent.EndRadioAnnouncement != null)
        {
            var mapName = Name(mapUid);
            var message = Loc.GetString(stationEvent.EndRadioAnnouncement, ("mapdestination", mapName));
            RadioSystem.SendRadioMessage(uid, message, stationEvent.EndRadioAnnouncementChannel, mapUid, escapeMarkup: false);
        }
        // End Frontier
    }

    /// <summary>
    ///     Called every tick when this event is running.
    ///     Events are responsible for their own lifetime, so this handles starting and ending after time.
    /// </summary>
    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StationEventComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var stationEvent, out var ruleData))
        {
            if (!GameTicker.IsGameRuleAdded(uid, ruleData))
                continue;

            if (!GameTicker.IsGameRuleActive(uid, ruleData) && !HasComp<DelayedStartRuleComponent>(uid))
            {
                GameTicker.StartGameRule(uid, ruleData);
            }
            else if (stationEvent.EndTime != null && Timing.CurTime >= stationEvent.EndTime && GameTicker.IsGameRuleActive(uid, ruleData))
            {
                GameTicker.EndGameRule(uid, ruleData);
            }
            // Frontier: Added Warning for events ending soon
            else if (!stationEvent.WarningAnnounced && stationEvent.EndTime != null && (stationEvent.EndTime.Value - Timing.CurTime).TotalSeconds <= stationEvent.WarningDurationLeft && GameTicker.IsGameRuleActive(uid, ruleData))
            {
                if (!TryGetRelevantMap(out _, out var mapUid))
                {
                    stationEvent.WarningAnnounced = true;
                    continue;
                }

                DispatchEventAnnouncement(uid, stationEvent, stationEvent.WarningAnnouncement, stationEvent.WarningAnnouncementColor, stationEvent.WarningAudio);
                if (stationEvent.WarningRadioAnnouncement != null)
                {
                    var mapName = Name(mapUid);
                    var message = Loc.GetString(stationEvent.WarningRadioAnnouncement, ("mapdestination", mapName));
                    RadioSystem.SendRadioMessage(uid, message, stationEvent.WarningRadioAnnouncementChannel, mapUid, escapeMarkup: false);
                }
                stationEvent.WarningAnnounced = true;
            }
            // End Frontier
        }
    }

    protected bool TryGetRandomStationForEvent(
        EntityUid eventUid,
        [NotNullWhen(true)] out EntityUid? station,
        Func<EntityUid, bool>? filter = null)
    {
        if (TryComp<StationEventComponent>(eventUid, out var ev) &&
            ev.AnnouncementTarget is { } existing &&
            Exists(existing) &&
            (filter == null || filter(existing)))
        {
            station = existing;
            BindEventStation(eventUid, existing, announceStart: true);
            return true;
        }

        if (!TryGetRandomStation(out station, filter) || station == null)
            return false;

        BindEventStation(eventUid, station.Value, announceStart: true);
        return true;
    }

    protected void BindEventStation(EntityUid eventUid, EntityUid station, bool announceStart = false)
    {
        if (!TryComp<StationEventComponent>(eventUid, out var ev))
            return;

        ev.AnnouncementTarget = station;
        if (!announceStart ||
            ev.AnnouncementScope != StationEventAnnouncementScope.Station ||
            ev.LocalStartAnnounced)
            return;

        DispatchEventAnnouncement(eventUid, ev, ev.StartAnnouncement, ev.StartAnnouncementColor, ev.StartAudio);
        ev.LocalStartAnnounced = true;
    }

    private void DispatchEventAnnouncement(
        EntityUid uid,
        StationEventComponent stationEvent,
        string? locId,
        Color color,
        SoundSpecifier? audio)
    {
        var senderKey = string.IsNullOrWhiteSpace(stationEvent.AnnouncementSender)
            ? "chat-manager-sender-announcement"
            : stationEvent.AnnouncementSender;
        var sender = Loc.TryGetString(senderKey, out var localizedSender) ? localizedSender : senderKey;

        string? message = null;
        if (locId != null)
        {
            var mapName = TryGetRelevantMap(out _, out var mapUid) ? Name(mapUid) : string.Empty;
            message = Loc.GetString(locId, ("mapdestination", mapName));
        }

        var filter = GetAnnouncementFilter(stationEvent);
        if (message != null)
            ChatSystem.DispatchFilteredAnnouncement(filter, message, uid, sender, playSound: false, colorOverride: color);

        if (audio != null)
            Audio.PlayGlobal(audio, filter, true);
    }

    private Filter GetAnnouncementFilter(StationEventComponent stationEvent)
    {
        switch (stationEvent.AnnouncementScope)
        {
            case StationEventAnnouncementScope.Global:
                return Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);
            case StationEventAnnouncementScope.Station:
                if (stationEvent.AnnouncementTarget is { } station &&
                    TryComp<StationDataComponent>(station, out var data))
                    return StationSystem.GetInStation(data);
                goto case StationEventAnnouncementScope.Map;
            case StationEventAnnouncementScope.Map:
            default:
                if (!TryGetRelevantMap(out var mapId, out _))
                    return Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);
                return Filter.Empty().AddInMap(mapId, EntityManager);
        }
    }
}
