// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Frontier.Parking;
using Content.Server._NF.Bank;
using Content.Server._NF.Shipyard.Systems;
using Content.Server.Popups;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Lua.Parking;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using System.Linq;
using static Content.Server._NF.Shipyard.Systems.ShipyardSystem;

namespace Content.Server._Lua.Parking;

public sealed class TrafficManagerTabletSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly FrontierParkingSystem _parking = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly DockingSystem _docking = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;

    private TimeSpan _nextUiUpdate;
    private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TrafficManagerTabletComponent, AfterActivatableUIOpenEvent>(OnOpen);
        SubscribeLocalEvent<TrafficManagerTabletComponent, TrafficManagerTabletUiMessage>(OnUiMessage);
        _nextUiUpdate = _timing.CurTime + UiUpdateInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUiUpdate) return;
        _nextUiUpdate = _timing.CurTime + UiUpdateInterval;
        var query = EntityQueryEnumerator<TrafficManagerTabletComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_ui.HasUi(uid, TrafficManagerTabletUiKey.Key)) continue;
            foreach (var actor in _ui.GetActors(uid, TrafficManagerTabletUiKey.Key))
            { UpdateUi(uid, actor); }
        }
    }

    private void OnOpen(EntityUid uid, TrafficManagerTabletComponent comp, AfterActivatableUIOpenEvent args)
    { UpdateUi(uid, args.User); }

    private void OnUiMessage(EntityUid uid, TrafficManagerTabletComponent comp, TrafficManagerTabletUiMessage msg)
    {
        var user = msg.Actor;
        if (!IsAuthorized(uid, user))
        {
            UpdateUi(uid, user);
            return;
        }
        var shuttleUid = GetEntity(msg.Shuttle);
        switch (msg.Action)
        {
            case TrafficManagerTabletAction.Refresh: break;
            case TrafficManagerTabletAction.ResetTimer:
                _parking.ResetTimer(shuttleUid); break;
            case TrafficManagerTabletAction.AddTenMinutes:
                _parking.AddTenMinutes(shuttleUid); break;
            case TrafficManagerTabletAction.Sell:
                TrySell(user, shuttleUid); break;
        }
        UpdateUi(uid, user);
    }

    private bool IsAuthorized(EntityUid tabletUid, EntityUid user)
    {
        if (!_accessReader.IsAllowed(user, tabletUid)) return false;
        return true;
    }

    private void UpdateUi(EntityUid uid, EntityUid user)
    {
        if (!_ui.HasUi(uid, TrafficManagerTabletUiKey.Key)) return;
        var authorized = IsAuthorized(uid, user);
        var error = authorized ? null : Loc.GetString("traffic-manager-tablet-error-unauthorized");
        var list = new List<TrafficManagerTabletShuttleEntry>();
        var station = _parking.FrontierStation;
        if (authorized && station != null)
        {
            foreach (var (shuttle, deed, state) in _parking.EnumerateTracked())
            {
                var elapsed = (int)(_timing.CurTime - state.CycleStart).TotalSeconds;
                var allowedSeconds = (int)TimeSpan.FromMinutes(10 + state.ExtraMinutes).TotalSeconds;
                var remaining = Math.Max(0, allowedSeconds - elapsed);
                var status = state.NeedsDisposal ? TrafficManagerShuttleStatus.Red : state.HasViolation ? TrafficManagerShuttleStatus.Orange : TrafficManagerShuttleStatus.Green;
                var shuttleName = ShipyardSystem.GetFullName(deed);
                var ownerName = deed.ShuttleOwner ?? "Unknown";
                var sellEnabled = state.NeedsDisposal;
                list.Add(new TrafficManagerTabletShuttleEntry(GetNetEntity(shuttle), shuttleName, ownerName, status, remaining, state.ExtraMinutes, state.NeedsDisposal, sellEnabled));
            }
        }
        list = list.OrderByDescending(s => s.Status).ThenBy(s => s.ShuttleName).ToList();
        var stateUi = new TrafficManagerTabletUiState(authorized, error, list);
        _ui.SetUiState(uid, TrafficManagerTabletUiKey.Key, stateUi);
    }

    private void TrySell(EntityUid user, EntityUid shuttleUid)
    {
        if (_parking.FrontierStation is not { } stationUid) return;
        if (!_parking.TryGetTracked(shuttleUid, out var state) || !state.NeedsDisposal) return;
        if (!TryComp<StationDataComponent>(stationUid, out var stationData)) return;
        var targetGrid = _station.GetLargestGrid(stationData);
        if (targetGrid == null) return;
        if (!IsDockedToGrid(shuttleUid, targetGrid.Value))
        { _popup.PopupEntity(Loc.GetString("traffic-manager-tablet-popup-must-be-docked"), user, user, PopupType.SmallCaution); return; }
        var result = _shipyard.TrySellShuttleToGrid(targetGrid.Value, shuttleUid, EntityUid.Invalid, out var bill);
        if (result.Error != ShipyardSaleError.Success)
        { _popup.PopupEntity(Loc.GetString("traffic-manager-tablet-popup-sell-failed-organics"), user, user, PopupType.SmallCaution); return; }
        if (bill > 0) _bank.TrySectorDeposit(SectorBankAccount.Frontier, bill, LedgerEntryType.StationDepositAssetsSold);
        _popup.PopupEntity(Loc.GetString("traffic-manager-tablet-popup-sold", ("amount", bill)), user, user, PopupType.Medium);
    }

    private bool IsDockedToGrid(EntityUid shuttleUid, EntityUid targetGrid)
    {
        foreach (var dock in _docking.GetDocks(shuttleUid))
        {
            if (dock.Comp.DockedWith == EntityUid.Invalid) continue;
            if (TryComp<TransformComponent>(dock.Comp.DockedWith, out var dXform) && dXform.GridUid == targetGrid) return true;
        }
        return false;
    }
}


