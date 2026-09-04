using System.Linq;
using Content.Shared._Lua.Announce;
using Content.Server._Lua.Sectors;
using Content.Server._NF.SectorServices;
using Content.Server.Chat.Systems;
using Content.Server.Station.Components;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.AlertLevel;

public sealed class AlertLevelSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;
    [Dependency] private readonly SectorSystem _sectorSystem = default!;

    public const string DefaultAlertLevelSet = "stationAlerts";

    public override void Initialize()
    {
        SubscribeLocalEvent<AlertLevelComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);
    }

    public override void Update(float time)
    {
        var query = EntityQueryEnumerator<AlertLevelComponent>();

        while (query.MoveNext(out _, out var alert))
        {
            if (alert.CurrentDelay <= 0)
            {
                if (alert.ActiveDelay)
                {
                    RaiseLocalEvent(new AlertLevelDelayFinishedEvent());
                    alert.ActiveDelay = false;
                }
                continue;
            }

            alert.CurrentDelay -= time;
        }
    }

    private void OnInit(EntityUid uid, AlertLevelComponent comp, ComponentInit args)
    {
        if (!_prototypeManager.TryIndex(comp.AlertLevelPrototype, out AlertLevelPrototype? alerts))
            return;

        comp.AlertLevels = alerts;

        var defaultLevel = comp.AlertLevels.DefaultLevel;
        if (string.IsNullOrEmpty(defaultLevel))
            defaultLevel = comp.AlertLevels.Levels.Keys.First();

        SetLevel(uid, defaultLevel, false, false, true);
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        if (!args.ByType.TryGetValue(typeof(AlertLevelPrototype), out var alertPrototypes)
            || !alertPrototypes.Modified.TryGetValue(DefaultAlertLevelSet, out var alertObject)
            || alertObject is not AlertLevelPrototype alerts)
        {
            return;
        }

        var query = EntityQueryEnumerator<AlertLevelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.AlertLevels = alerts;

            if (!comp.AlertLevels.Levels.ContainsKey(comp.CurrentLevel))
            {
                var defaultLevel = comp.AlertLevels.DefaultLevel;
                if (string.IsNullOrEmpty(defaultLevel))
                    defaultLevel = comp.AlertLevels.Levels.Keys.First();

                SetLevel(uid, defaultLevel, true, true, true);
            }
        }

        RaiseLocalEvent(new AlertLevelPrototypeReloadedEvent());
    }

    public bool TryResolveAlert(EntityUid context, out EntityUid service, out AlertLevelComponent alert, out MapId mapId)
    {
        service = default;
        alert = default!;
        mapId = MapId.Nullspace;

        if (_sectorService.TryGetMapId(context, out mapId)
            && TryComp(context, out AlertLevelComponent? serviceAlert))
        {
            service = context;
            alert = serviceAlert;
            return true;
        }

        if (TryComp(context, out TransformComponent? xform)
            && xform.MapID != MapId.Nullspace
            && _sectorService.TryGetServiceEntity(xform.MapID, out service)
            && TryComp(service, out AlertLevelComponent? mapAlert)
            && _sectorService.TryGetMapId(service, out mapId))
        {
            alert = mapAlert;
            return true;
        }

        if (TryComp(context, out StationDataComponent? stationData))
        {
            foreach (var gridUid in stationData.Grids)
            {
                if (!TryComp(gridUid, out TransformComponent? gridXform)
                    || gridXform.MapID == MapId.Nullspace)
                    continue;

                if (!_sectorService.TryGetServiceEntity(gridXform.MapID, out service)
                    || !TryComp(service, out AlertLevelComponent? stationAlert)
                    || !_sectorService.TryGetMapId(service, out mapId))
                    continue;

                alert = stationAlert;
                return true;
            }
        }

        return false;
    }

    public string GetLevel(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (alert != null)
            return alert.CurrentLevel;

        if (!TryResolveAlert(station, out _, out var resolved, out _))
            return string.Empty;

        return resolved.CurrentLevel;
    }

    public float GetAlertLevelDelay(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (alert != null)
            return alert.CurrentDelay;

        if (!TryResolveAlert(station, out _, out var resolved, out _))
            return float.NaN;

        return resolved.CurrentDelay;
    }

    public string GetDefaultLevel(Entity<AlertLevelComponent?> station)
    {
        if (!Resolve(station.Owner, ref station.Comp) || station.Comp.AlertLevels == null)
            return string.Empty;
        return station.Comp.AlertLevels.DefaultLevel;
    }

    public void SetLevelGlobal(string level, bool playSound, bool announce, bool force = false, bool locked = false)
    {
        foreach (var (mapId, service) in _sectorService.GetServicesWithMaps())
            SetLevel(service, level, playSound, announce, force, locked, mapOverride: mapId);
    }

    public bool SetLevel(EntityUid station, string level, bool playSound, bool announce, bool force = false,
        bool locked = false, MetaDataComponent? dataComponent = null, AlertLevelComponent? component = null,
        MapId? mapOverride = null)
    {
        EntityUid service;
        MapId mapId;

        if (component != null)
        {
            service = station;
            if (mapOverride != null)
                mapId = mapOverride.Value;
            else if (_sectorService.TryGetMapId(station, out mapId))
            {
            }
            else if (TryComp(station, out TransformComponent? tx) && tx.MapID != MapId.Nullspace)
                mapId = tx.MapID;
            else
                return false;
        }
        else if (!TryResolveAlert(station, out service, out component, out mapId))
        {
            return false;
        }
        else if (mapOverride != null)
        {
            mapId = mapOverride.Value;
        }

        if (component.AlertLevels == null
            || !component.AlertLevels.Levels.TryGetValue(level, out var detail))
        {
            return false;
        }

        if (component.CurrentLevel == level)
            return true;

        if (!force)
        {
            if (!detail.Selectable
                || component.CurrentDelay > 0
                || component.IsLevelLocked)
            {
                return false;
            }

            component.CurrentDelay = _cfg.GetCVar(CCVars.GameAlertLevelChangeDelay);
            component.ActiveDelay = true;
        }

        component.CurrentLevel = level;
        component.IsLevelLocked = locked;

        var sectorName = _sectorSystem.GetSectorDisplayName(mapId);

        var name = level.ToLower();
        if (Loc.TryGetString($"alert-level-{level}", out var locName))
            name = locName.ToLower();

        var announcement = detail.Announcement;
        if (Loc.TryGetString(detail.Announcement, out var locAnnouncement, ("sector", sectorName)))
            announcement = locAnnouncement;

        var announcementFull = Loc.GetString("alert-level-announcement",
            ("name", name),
            ("announcement", announcement),
            ("sector", sectorName));

        var filter = Filter.Empty().AddInMap(mapId, EntityManager);
        var playDefault = false;
        if (playSound)
        {
            if (detail.Sound != null)
                _audio.PlayGlobal(detail.Sound, filter, true, detail.Sound.Params);
            else
                playDefault = true;
        }

        if (announce)
        {
            var sender = Name(service);
            if (Resolve(station, ref dataComponent, false))
                sender = dataComponent.EntityName;
            else if (TryComp(station, out MetaDataComponent? meta))
                sender = meta.EntityName;

            _chatSystem.DispatchFilteredAnnouncement(filter, announcementFull, station, sender,
                playSound: playDefault, colorOverride: detail.Color, announcementPreset: AnnouncementOverlayParams.PresetAlert);
        }

        RaiseLocalEvent(new AlertLevelChangedEvent(service, mapId, level));
        return true;
    }
}

public sealed class AlertLevelDelayFinishedEvent : EntityEventArgs;

public sealed class AlertLevelPrototypeReloadedEvent : EntityEventArgs;

public sealed class AlertLevelChangedEvent : EntityEventArgs
{
    public EntityUid Station { get; }
    public MapId MapId { get; }
    public string AlertLevel { get; }

    public AlertLevelChangedEvent(EntityUid station, MapId mapId, string alertLevel)
    {
        Station = station;
        MapId = mapId;
        AlertLevel = alertLevel;
    }
}
