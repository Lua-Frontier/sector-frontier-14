using Content.Server._Lua.StationRecords.Systems;
using Content.Server._NF.Station.Components;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Components;
using Content.Server.Popups;
using Content.Shared._Lua.StationRecords;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.StationRecords;
using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.StationRecords.Systems;

public sealed class GeneralStationRecordConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly StationJobsSystem _stationJobsSystem = default!; // Frontier
    [Dependency] private readonly IAdminLogManager _adminLog = default!; // Frontier
    [Dependency] private readonly ShipCrewAssignmentSystem _shipCrew = default!;// Lua
    [Dependency] private readonly PopupSystem _popup = default!; // Lua

    public override void Initialize()
    {
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, RecordModifiedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, AfterGeneralRecordCreatedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, RecordRemovedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, EntInsertedIntoContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, EntRemovedFromContainerMessage>(UpdateUserInterface);
        Subs.BuiEvents<GeneralStationRecordConsoleComponent>(GeneralStationRecordConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<SelectStationRecord>(OnKeySelected);
            subs.Event<SetStationRecordFilter>(OnFiltersChanged);
            subs.Event<DeleteStationRecord>(OnRecordDelete);
            subs.Event<AdjustStationJobMsg>(OnAdjustJob); // Frontier
            subs.Event<SetStationAdvertisementMsg>(OnAdvertisementChanged); // Frontier
            subs.Event<AssignShipCrewRoleMsg>(OnAssignShipCrewRole); // Lua
            subs.Event<FireShipCrewByRecordMsg>(OnFireShipCrewByRecord); // Lua
            subs.Event<SetShipCrewRoleByNameMsg>(OnSetShipCrewRoleByName); // Lua
            subs.Event<FireShipCrewByNameMsg>(OnFireShipCrewByName); // Lua
        });
    }

    private void OnRecordDelete(Entity<GeneralStationRecordConsoleComponent> ent, ref DeleteStationRecord args)
    {
        if (!ent.Comp.CanDeleteEntries)
            return;

        var owning = _station.GetOwningStation(ent.Owner);

        if (owning != null)
            _stationRecords.RemoveRecord(new StationRecordKey(args.Id, owning.Value));
        UpdateUserInterface(ent); // Apparently an event does not get raised for this.
    }

    private void UpdateUserInterface<T>(Entity<GeneralStationRecordConsoleComponent> ent, ref T args)
    {
        UpdateUserInterface(ent);
    }

    // TODO: instead of copy paste shitcode for each record console, have a shared records console comp they all use
    // then have this somehow play nicely with creating ui state
    // if that gets done put it in StationRecordsSystem console helpers section :)
    private void OnKeySelected(Entity<GeneralStationRecordConsoleComponent> ent, ref SelectStationRecord msg)
    {
        ent.Comp.ActiveKey = msg.SelectedKey;
        UpdateUserInterface(ent);
    }

    // Frontier: job counts, advertisements
    private void OnAdjustJob(Entity<GeneralStationRecordConsoleComponent> ent, ref AdjustStationJobMsg msg)
    {
        if (!IsAdminObserver(msg.Actor))
        {
            UpdateUserInterface(ent);
            return;
        }

        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is EntityUid station)
        {
            _stationJobsSystem.TryAdjustJobSlot(station, msg.JobProto, msg.Amount, false, true);
            UpdateUserInterface(ent);
        }
    }

    private bool IsAdminObserver(EntityUid uid)
    {
        var proto = MetaData(uid).EntityPrototype;
        return proto != null && proto.ID == GameTicker.AdminObserverPrototypeName;
    }
    private void OnFiltersChanged(Entity<GeneralStationRecordConsoleComponent> ent, ref SetStationRecordFilter msg)
    {
        if (ent.Comp.Filter == null ||
            ent.Comp.Filter.Type != msg.Type || ent.Comp.Filter.Value != msg.Value)
        {
            ent.Comp.Filter = new StationRecordsFilter(msg.Type, msg.Value);
            UpdateUserInterface(ent);
        }
    }

    private void OnAdvertisementChanged(Entity<GeneralStationRecordConsoleComponent> ent, ref SetStationAdvertisementMsg msg)
    {
        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is EntityUid station
            && TryComp<ExtraShuttleInformationComponent>(station, out var vesselInfo))
        {
            vesselInfo.Advertisement = msg.Advertisement;
            _adminLog.Add(LogType.ShuttleInfoChanged, $"{ToPrettyString(msg.Actor):actor} set their shuttle {ToPrettyString(station)}'s ad text to {vesselInfo.Advertisement}");
            UpdateUserInterface(ent);
            _stationJobsSystem.UpdateJobsAvailable(); // Nasty - ideally this sends out partial information - one ship changed its advertisement.
        }
    }
    // End Frontier: job counts, advertisements

    private void UpdateUserInterface(Entity<GeneralStationRecordConsoleComponent> ent)
    {
        var (uid, console) = ent;
        var owningStation = _station.GetOwningStation(uid);

        // Frontier: jobs, advertisements
        IReadOnlyDictionary<ProtoId<JobPrototype>, int?>? jobList = null;
        string? advertisement = null;
        if (owningStation != null)
        {
            jobList = _stationJobsSystem.GetJobs(owningStation.Value);
            if (TryComp<ExtraShuttleInformationComponent>(owningStation, out var extraVessel))
                advertisement = extraVessel.Advertisement;
        }

        var isCaptainIdPresent = console.CaptainIdSlot.Item is { Valid: true };
        var isTargetIdPresent = console.TargetIdSlot.Item is { Valid: true };
        string? captainIdName = null;
        if (console.CaptainIdSlot.Item is { Valid: true } captainId)
        {
            if (TryComp<IdCardComponent>(captainId, out var card))
            {
                var job = string.IsNullOrWhiteSpace(card.LocalizedJobTitle) ? Loc.GetString("generic-not-available-shorthand") : card.LocalizedJobTitle;
                captainIdName = $"{card.FullName}, ({job})";
            }
            else { captainIdName = MetaData(captainId).EntityName; }
        }
        var captainShipName = GetCaptainShipNameIfAuthorized(uid, console, owningStation);
        string? targetIdName = null;
        string? targetAssignedShipName = null;
        string? targetAssignedRoleLocKey = null;
        if (console.TargetIdSlot.Item is { Valid: true } targetId)
        {
            if (TryComp<IdCardComponent>(targetId, out var card))
            {
                var job = string.IsNullOrWhiteSpace(card.LocalizedJobTitle) ? Loc.GetString("generic-not-available-shorthand") : card.LocalizedJobTitle;
                targetIdName = $"{card.FullName}, ({job})";
            }
            else { targetIdName = MetaData(targetId).EntityName; }
            if (_shipCrew.TryGetAssignment(targetId, out var info))
            {
                targetAssignedShipName = info.shipName;
                targetAssignedRoleLocKey = info.roleLocKey;
            }
        }
        List<ShipCrewRosterEntry>? shipRoster = null;
        if (TryGetAuthorizedCaptainShip(uid, console, out var shuttleUid, out _)) shipRoster = _shipCrew.GetRosterForShuttle(shuttleUid);
        if (!TryComp<StationRecordsComponent>(owningStation, out var stationRecords))
        {
            _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, new GeneralStationRecordConsoleState(null, null, null, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement, isCaptainIdPresent, captainIdName, captainShipName, isTargetIdPresent, targetIdName, targetAssignedShipName, targetAssignedRoleLocKey, shipRoster));
            return;
        }

        var listing = _stationRecords.BuildListing((owningStation.Value, stationRecords), console.Filter);

        switch (listing.Count)
        {
            case 0:
                var consoleState = new GeneralStationRecordConsoleState(null, null, null, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement, isCaptainIdPresent, captainIdName, captainShipName, isTargetIdPresent, targetIdName, targetAssignedShipName, targetAssignedRoleLocKey, shipRoster);
                _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, consoleState);
                return;
            default:
                if (console.ActiveKey == null)
                    console.ActiveKey = listing.Keys.First();
                break;
        }

        if (console.ActiveKey is not { } id)
        {
            _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, new GeneralStationRecordConsoleState(null, null, listing, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement, isCaptainIdPresent, captainIdName, captainShipName, isTargetIdPresent, targetIdName, targetAssignedShipName, targetAssignedRoleLocKey, shipRoster));
            return;
        }

        var key = new StationRecordKey(id, owningStation.Value);
        _stationRecords.TryGetRecord<GeneralStationRecord>(key, out var record, stationRecords);

        GeneralStationRecordConsoleState newState = new(id, record, listing, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement, isCaptainIdPresent, captainIdName, captainShipName, isTargetIdPresent, targetIdName, targetAssignedShipName, targetAssignedRoleLocKey, shipRoster);
        _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, newState);
    }

    private void OnAssignShipCrewRole(Entity<GeneralStationRecordConsoleComponent> ent, ref AssignShipCrewRoleMsg msg)
    {
        if (msg.Actor is not { Valid: true }) return;
        if (!TryGetAuthorizedCaptainShip(ent.Owner, ent.Comp, out var shuttleUid, out var shipName))
        {
            UpdateUserInterface(ent);
            return;
        }
        if (ent.Comp.TargetIdSlot.Item is not { Valid: true } targetId)
        {
            UpdateUserInterface(ent);
            return;
        }
        if (!_shipCrew.TryAssign(targetId, shuttleUid, shipName, msg.Role, out var existingShipName))
        {
            _popup.PopupEntity(Loc.GetString("ship-crew-console-already-assigned", ("ship", existingShipName ?? Loc.GetString("generic-not-available-shorthand"))), msg.Actor);
            UpdateUserInterface(ent);
            return;
        }
        UpdateUserInterface(ent);
    }

    private void OnFireShipCrewByRecord(Entity<GeneralStationRecordConsoleComponent> ent, ref FireShipCrewByRecordMsg msg)
    {
        if (msg.Actor is not { Valid: true }) return;
        if (!TryGetAuthorizedCaptainShip(ent.Owner, ent.Comp, out var shuttleUid, out _))
        {
            UpdateUserInterface(ent);
            return;
        }
        var owningStation = _station.GetOwningStation(ent.Owner);
        if (owningStation is not { Valid: true } stationUid)
        {
            UpdateUserInterface(ent);
            return;
        }
        if (!TryComp<StationRecordsComponent>(stationUid, out var stationRecords))
        {
            UpdateUserInterface(ent);
            return;
        }
        var key = new StationRecordKey(msg.RecordId, stationUid);
        if (!_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var record, stationRecords))
        {
            UpdateUserInterface(ent);
            return;
        }
        _shipCrew.ClearForShuttleAndName(shuttleUid, record.Name);
        UpdateUserInterface(ent);
    }

    private void OnSetShipCrewRoleByName(Entity<GeneralStationRecordConsoleComponent> ent, ref SetShipCrewRoleByNameMsg msg)
    {
        if (msg.Actor is not { Valid: true }) return;
        if (!TryGetAuthorizedCaptainShip(ent.Owner, ent.Comp, out var shuttleUid, out _))
        {
            UpdateUserInterface(ent);
            return;
        }
        _shipCrew.TrySetRoleForShuttleAndName(shuttleUid, msg.Name, msg.Role);
        UpdateUserInterface(ent);
    }

    private void OnFireShipCrewByName(Entity<GeneralStationRecordConsoleComponent> ent, ref FireShipCrewByNameMsg msg)
    {
        if (msg.Actor is not { Valid: true }) return;
        if (!TryGetAuthorizedCaptainShip(ent.Owner, ent.Comp, out var shuttleUid, out _))
        {
            UpdateUserInterface(ent);
            return;
        }
        _shipCrew.ClearForShuttleAndName(shuttleUid, msg.Name);
        UpdateUserInterface(ent);
    }

    private bool TryGetAuthorizedCaptainShip(EntityUid consoleUid, GeneralStationRecordConsoleComponent console, out EntityUid shuttleUid, out string shipName)
    {
        shuttleUid = default;
        shipName = string.Empty;
        if (console.CaptainIdSlot.Item is not { Valid: true } captainId) return false;
        if (!TryComp<ShuttleDeedComponent>(captainId, out var deed) || deed.ShuttleUid is not { Valid: true } shuttle) return false;
        var consoleStation = _station.GetOwningStation(consoleUid);
        if (consoleStation is not { Valid: true }) return false;
        var shuttleStation = _station.GetOwningStation(shuttle);
        if (shuttleStation is not { Valid: true } || shuttleStation.Value != consoleStation.Value) return false;
        shuttleUid = shuttle;
        shipName = GetFullName(deed);
        return true;
    }

    private string? GetCaptainShipNameIfAuthorized(EntityUid consoleUid, GeneralStationRecordConsoleComponent console, EntityUid? owningStation)
    {
        if (owningStation is not { Valid: true }) return null;
        if (console.CaptainIdSlot.Item is not { Valid: true } captainId) return null;
        if (!TryComp<ShuttleDeedComponent>(captainId, out var deed) || deed.ShuttleUid is not { Valid: true } shuttle) return null;
        var shuttleStation = _station.GetOwningStation(shuttle);
        if (shuttleStation is not { Valid: true } || shuttleStation.Value != owningStation.Value) return null;
        return GetFullName(deed);
    }

    private static string GetFullName(ShuttleDeedComponent comp)
    {
        string?[] parts = { comp.ShuttleName, comp.ShuttleNameSuffix };
        return string.Join(' ', parts.Where(it => !string.IsNullOrWhiteSpace(it)));
    }
}
