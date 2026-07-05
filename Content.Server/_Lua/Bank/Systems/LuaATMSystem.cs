// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Linq;
using Robust.Shared.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Audio;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Events;
using Content.Shared._Lua.Bank.Events;
using Content.Shared._NF.Bank.Components;
using Content.Shared._Lua.Bank.UI;
using Content.Shared._Lua.Bank;
using Content.Shared.Database;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Content.Server.Popups;
using Content.Server._NF.Bank;
using Content.Server.Hands.Systems;
using Content.Server.Administration.Logs;

namespace Content.Server._Lua.Bank.Systems;

public sealed class LuaATMSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IAdminLogManager _admin = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BankATMComponent, EntInsertedIntoContainerMessage>(OnCashSlotChanged);
        SubscribeLocalEvent<BankATMComponent, EntRemovedFromContainerMessage>(OnCashSlotChanged);

        Subs.BuiEvents<BankATMComponent>(BankATMMenuUiKey.Key, subs =>
        {
            subs.Event<BankWithdrawMessage>(OnWithdraw);
            subs.Event<BankDepositMessage>(OnDeposit);
            subs.Event<BoundUIOpenedEvent>(OnUIOpen);
        });
    }

    private void OnUIOpen(EntityUid atm, BankATMComponent atmComp, BoundUIOpenedEvent args)
    {
        UpdateUserInterface(atm, atmComp);
    }

    private void OnCashSlotChanged(EntityUid atm, BankATMComponent atmComp, ContainerModifiedMessage args)
    {
        UpdateUserInterface(atm, atmComp);
    }

    private void OnWithdraw(EntityUid atm, BankATMComponent atmComp, BankWithdrawMessage args)
    {
        var player = args.Actor;

        if (!_bank.TryGetBalance(player, out var balance))
        {
            Log.Info($"{player} has no bank account");
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-no-bank"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        if (balance < args.Amount)
        {
            _popup.PopupEntity(Loc.GetString("bank-insufficient-funds"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        if (!_bank.TryBankWithdraw(player, args.Amount))
        {
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-transaction-denied"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        _popup.PopupEntity(Loc.GetString("bank-atm-menu-withdraw-successful"), atm, player);
        _audio.PlayPvs(atmComp.ConfirmSound, atm);
        _admin.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(player):actor} withdrew {args.Amount} from {ToPrettyString(atm)}");

        var cashStack = _stack.Spawn(args.Amount, atmComp.CashType, Transform(player).Coordinates);
        _hands.PickupOrDrop(player, cashStack);

        UpdateUserInterface(atm, atmComp);
    }

    private void OnDeposit(EntityUid atm, BankATMComponent atmComp, BankDepositMessage args)
    {
        var player = args.Actor;

        if (atmComp.WithdrawOnly)
        {
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-only-withdraw"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        if (!_bank.TryGetBalance(player, out _))
        {
            Log.Info($"{player} has no bank account");
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-no-bank"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        if (!TryComp<StackComponent>(atmComp.CashSlot.ContainerSlot?.ContainedEntity, out var stackComponent))
        {
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-wrong-cash"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        if (stackComponent.StackTypeId != atmComp.CashType)
        {
            Log.Info($"{stackComponent.StackTypeId} is not {atmComp.CashType}");
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-wrong-cash"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        var originalDeposit = GetDepositValue(atmComp, out var depositItem);
        var deposit = originalDeposit;

        foreach (var (account, taxCoeff) in atmComp.TaxAccounts)
        {
            if (!float.IsFinite(taxCoeff) || taxCoeff <= 0.0f)
            {
                continue;
            }

            var tax = (int)Math.Floor(originalDeposit * taxCoeff);
            deposit -= tax;
            _bank.TrySectorDeposit(account, tax, LedgerEntryType.BlackMarketAtmTax);
        }

        deposit = Math.Max(0, deposit);

        if (!_bank.TryBankDeposit(player, deposit))
        {
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-transaction-denied"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        _popup.PopupEntity(Loc.GetString("bank-atm-menu-deposit-successful"), atm, player);
        _audio.PlayPvs(atmComp.ConfirmSound, atm);
        _admin.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(player):actor} deposited {deposit} into {ToPrettyString(atm)}");

        QueueDel(depositItem);
        UpdateUserInterface(atm, atmComp);
    }

    private void UpdateUserInterface(EntityUid atm, BankATMComponent? atmComp = null)
    {
        if (!Resolve(atm, ref atmComp)
            || !_userInterface.HasUi(atm, BankATMMenuUiKey.Key))
        {
            return;
        }

        var deposit = GetDepositValue(atmComp, out _);
        var state = new LuaATMMenuInterfaceState(atmComp.Corrupted, atmComp.WithdrawOnly, deposit);

        _userInterface.SetUiState(atm, BankATMMenuUiKey.Key, state);

        var actors = _userInterface.GetActors(atm, BankATMMenuUiKey.Key);

        foreach (var actor in actors)
        {
            var enabled = _bank.TryGetBalance(actor, out var bankBalance);
            var balance = enabled ? bankBalance : 0;
            var yupiCode = _bank.EnsureYupiForEntity(actor);
            var history = GetOperationHistory(actor);

            var personalMessage = new LuaATMPersonalInfoMessage(enabled, balance, yupiCode, history);
            _userInterface.ServerSendUiMessage(atm, BankATMMenuUiKey.Key, personalMessage, actor);
        }
    }

    private int GetDepositValue(BankATMComponent atmComp, out EntityUid? depositItem)
    {
        depositItem = atmComp.CashSlot.ContainerSlot?.ContainedEntity;

        if (depositItem == null)
        {
            return 0;
        }

        if (!TryComp<StackComponent>(depositItem, out var cashStack)
            || cashStack.StackTypeId != atmComp.CashType)
        {
            return -1;
        }

        return cashStack.Count;
    }

    private List<BankAccountOperation> GetOperationHistory(EntityUid mobUid)
    {
        if (!TryComp<BankAccountComponent>(mobUid, out var bank))
        {
            return [];
        }

        return bank.OperationHistory.Count > 10 ? [.. bank.OperationHistory.TakeLast(10)] : bank.OperationHistory;
    }
}
