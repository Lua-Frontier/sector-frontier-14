// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Company;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Lua.CLVar;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Server.Hands.Systems;
using Content.Server.Stack;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class SectorPayoutSystem : SharedSectorPayoutSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly FactionOwnedStationSystem _ownedStations = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private int _intervalSeconds = 3600;
    private int _perStation = 1;

    private readonly Dictionary<string, FactionPayoutLedger> _ledgers = new(StringComparer.Ordinal);

    private sealed class FactionPayoutLedger
    {
        public int Accumulated;
        public TimeSpan LastAccrualAt;
        public List<PayoutClaimHistoryEntry> ClaimHistory = new();
    }

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CLVars.StationPayoutIntervalSeconds, v => _intervalSeconds = Math.Max(1, v), true);
        Subs.CVar(_cfg, CLVars.StationPayoutPerStation, v => _perStation = Math.Max(0, v), true);

        SubscribeLocalEvent<FactionPayoutCollectorComponent, EntInsertedIntoContainerMessage>(OnCashSlotChanged);
        SubscribeLocalEvent<FactionPayoutCollectorComponent, EntRemovedFromContainerMessage>(OnCashSlotChanged);

        Subs.BuiEvents<FactionPayoutCollectorComponent>(PayoutCollectorUiKey.Key, subs =>
        {
            subs.Event<PayoutCollectorWithdrawMessage>(OnWithdraw);
            subs.Event<PayoutCollectorDepositMessage>(OnDeposit);
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
        });
    }

    private void OnCashSlotChanged(EntityUid uid, FactionPayoutCollectorComponent comp, ContainerModifiedMessage args)
    {
        if (args.Container.ID != FactionPayoutCollectorComponent.CashSlotId)
            return;
        PushUi((uid, comp));
    }

    private void OnUiOpened(Entity<FactionPayoutCollectorComponent> ent, ref BoundUIOpenedEvent args)
    {
        PushUi(ent);
    }

    private void OnWithdraw(Entity<FactionPayoutCollectorComponent> ent, ref PayoutCollectorWithdrawMessage msg)
    {
        if (string.IsNullOrWhiteSpace(ent.Comp.Faction) || msg.Amount <= 0)
        {
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            PushUi(ent);
            return;
        }

        AccrueFaction(ent.Comp.Faction);
        var ledger = GetOrCreateLedger(ent.Comp.Faction);
        if (ledger.Accumulated < msg.Amount)
        {
            _popup.PopupEntity(Loc.GetString("payout-insufficient-funds"), ent.Owner, msg.Actor);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            PushUiForFaction(ent.Comp.Faction);
            return;
        }

        ledger.Accumulated -= msg.Amount;
        AddHistory(ledger, Identity.Name(msg.Actor, EntityManager), msg.Amount, isDeposit: false);

        var cash = _stack.Spawn(msg.Amount, ent.Comp.CashType, Transform(msg.Actor).Coordinates);
        _hands.PickupOrDrop(msg.Actor, cash);

        _popup.PopupEntity(Loc.GetString("payout-withdraw-successful"), ent.Owner, msg.Actor);
        _audio.PlayPvs(ent.Comp.ConfirmSound, ent.Owner);
        PushUiForFaction(ent.Comp.Faction);
    }

    private void OnDeposit(Entity<FactionPayoutCollectorComponent> ent, ref PayoutCollectorDepositMessage msg)
    {
        if (string.IsNullOrWhiteSpace(ent.Comp.Faction))
        {
            PushUi(ent);
            return;
        }

        AccrueFaction(ent.Comp.Faction);

        if (!TryGetInsertedCash(ent.Comp, out var cashEntity, out var stack) || stack.Count <= 0)
        {
            _popup.PopupEntity(Loc.GetString("payout-deposit-empty"), ent.Owner, msg.Actor);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            PushUi(ent);
            return;
        }

        if (stack.StackTypeId != ent.Comp.CashType)
        {
            _popup.PopupEntity(Loc.GetString("payout-wrong-cash"), ent.Owner, msg.Actor);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            PushUi(ent);
            return;
        }

        if (ent.Comp.CashSlot.ContainerSlot is not { } cashSlot ||
            !_container.Remove(cashEntity, cashSlot))
        {
            _popup.PopupEntity(Loc.GetString("payout-transaction-denied"), ent.Owner, msg.Actor);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent.Owner);
            PushUi(ent);
            return;
        }

        var amount = stack.Count;
        var ledger = GetOrCreateLedger(ent.Comp.Faction);
        ledger.Accumulated += amount;
        AddHistory(ledger, Identity.Name(msg.Actor, EntityManager), amount, isDeposit: true);
        QueueDel(cashEntity);

        _popup.PopupEntity(Loc.GetString("payout-deposit-successful"), ent.Owner, msg.Actor);
        _audio.PlayPvs(ent.Comp.ConfirmSound, ent.Owner);
        PushUiForFaction(ent.Comp.Faction);
    }

    private static void AddHistory(FactionPayoutLedger ledger, string name, int amount, bool isDeposit)
    {
        ledger.ClaimHistory.Insert(0, new PayoutClaimHistoryEntry(name, amount, isDeposit));
        if (ledger.ClaimHistory.Count > FactionPayoutCollectorComponent.MaxClaimHistory)
            ledger.ClaimHistory.RemoveRange(
                FactionPayoutCollectorComponent.MaxClaimHistory,
                ledger.ClaimHistory.Count - FactionPayoutCollectorComponent.MaxClaimHistory);
    }

    private bool TryGetInsertedCash(
        FactionPayoutCollectorComponent comp,
        out EntityUid cashEntity,
        [NotNullWhen(true)] out StackComponent? stack)
    {
        cashEntity = default;
        stack = null;
        var item = comp.CashSlot.ContainerSlot?.ContainedEntity;
        if (item == null || !TryComp(item.Value, out stack))
            return false;

        cashEntity = item.Value;
        return true;
    }

    private int GetDepositValue(FactionPayoutCollectorComponent comp)
    {
        if (!TryGetInsertedCash(comp, out _, out var stack))
            return 0;
        if (stack.StackTypeId != comp.CashType)
            return -1;
        return stack.Count;
    }

    private bool AccrueFaction(string faction)
    {
        var now = _timing.CurTime;
        var interval = TimeSpan.FromSeconds(Math.Max(1, _intervalSeconds));
        var ledger = GetOrCreateLedger(faction);

        if (ledger.LastAccrualAt == TimeSpan.Zero)
            ledger.LastAccrualAt = now - interval;

        var elapsed = now - ledger.LastAccrualAt;
        if (elapsed < interval)
            return false;

        var ticks = (int) (elapsed / interval);
        var owned = _ownedStations.CountOwnedStations(faction);
        if (owned > 0 && _perStation > 0 && ticks > 0)
            ledger.Accumulated += ticks * owned * _perStation;

        ledger.LastAccrualAt += interval * ticks;
        return true;
    }

    private FactionPayoutLedger GetOrCreateLedger(string faction)
    {
        if (!_ledgers.TryGetValue(faction, out var ledger))
        {
            ledger = new FactionPayoutLedger();
            _ledgers[faction] = ledger;
        }

        return ledger;
    }

    private void PushUiForFaction(string faction)
    {
        var q = AllEntityQuery<FactionPayoutCollectorComponent>();
        while (q.MoveNext(out var uid, out var comp))
        {
            if (!string.Equals(comp.Faction, faction, StringComparison.Ordinal))
                continue;
            SetUiState((uid, comp));
        }
    }

    private void PushUi(Entity<FactionPayoutCollectorComponent> ent)
    {
        var accrued = !string.IsNullOrWhiteSpace(ent.Comp.Faction) && AccrueFaction(ent.Comp.Faction);
        if (accrued)
        {
            PushUiForFaction(ent.Comp.Faction);
            return;
        }

        SetUiState(ent);
    }

    private void SetUiState(Entity<FactionPayoutCollectorComponent> ent)
    {
        var ledger = string.IsNullOrWhiteSpace(ent.Comp.Faction)
            ? null
            : GetOrCreateLedger(ent.Comp.Faction);

        var owned = _ownedStations.CountOwnedStations(ent.Comp.Faction);
        var interval = TimeSpan.FromSeconds(Math.Max(1, _intervalSeconds));
        var last = ledger == null || ledger.LastAccrualAt == TimeSpan.Zero
            ? _timing.CurTime
            : ledger.LastAccrualAt;
        var nextPayoutAt = last + interval;
        var history = ledger == null
            ? new List<PayoutClaimHistoryEntry>()
            : ledger.ClaimHistory.Select(e => new PayoutClaimHistoryEntry(e.CharacterName, e.Amount, e.IsDeposit)).ToList();
        var state = new PayoutCollectorBuiState(
            owned,
            ledger?.Accumulated ?? 0,
            GetDepositValue(ent.Comp),
            ent.Comp.Faction,
            _perStation,
            _intervalSeconds,
            nextPayoutAt,
            history);
        _ui.SetUiState(ent.Owner, PayoutCollectorUiKey.Key, state);
    }
}
