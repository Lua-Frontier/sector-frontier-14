// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Linq;
using System.Threading.Tasks;
using Robust.Shared.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Audio;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared._NF.Bank.Events;
using Content.Lua.Shared.Bank.Events;
using Content.Lua.Shared.Bank.UI;
using Content.Lua.Shared.Bank;
using Content.Shared.Database;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Content.Server.Popups;
using Content.Lua.Server.Bank;
using Content.Server.Hands.Systems;
using Content.Server.Administration.Logs;
using Robust.Shared.Asynchronous;
using Robust.Shared.Player;

namespace Content.Lua.Server.Bank.Systems;

public sealed class LuaATMSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IAdminLogManager _admin = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;

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

        if (depositItem is not { Valid: true } cashEntity ||
            atmComp.CashSlot.ContainerSlot is not BaseContainer cashSlot)
        {
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-transaction-denied"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        foreach (var (account, taxCoeff) in atmComp.TaxAccounts)
        {
            if (!float.IsFinite(taxCoeff) || taxCoeff <= 0.0f)
            {
                continue;
            }

            var tax = (int)Math.Floor(originalDeposit * taxCoeff);
            deposit -= tax;
            _bank.TrySectorDeposit(account, tax, LedgerEntryType.BlackMarketAtmTax, atm);
        }

        deposit = Math.Max(0, deposit);

        if (deposit <= 0)
        {
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-transaction-denied"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }
        if (!_container.Remove(cashEntity, cashSlot))
        {
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-transaction-denied"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }
        if (!_bank.TryBankDeposit(player, deposit))
        {
            _container.Insert(cashEntity, cashSlot);
            _popup.PopupEntity(Loc.GetString("bank-atm-menu-transaction-denied"), atm, player);
            _audio.PlayPvs(atmComp.ErrorSound, atm);
            UpdateUserInterface(atm, atmComp);
            return;
        }

        _popup.PopupEntity(Loc.GetString("bank-atm-menu-deposit-successful"), atm, player);
        _audio.PlayPvs(atmComp.ConfirmSound, atm);
        _admin.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(player):actor} deposited {deposit} into {ToPrettyString(atm)}");

        QueueDel(cashEntity);
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
            _ = RefreshPersonalInfoAsync(atm, actor);
        }
    }

    private async Task RefreshPersonalInfoAsync(EntityUid atm, EntityUid actor)
    {
        var galBankCode = string.Empty;
        if (_player.TryGetSessionByEntity(actor, out var session))
            galBankCode = await _bank.FetchGalBankCodeAsync(session.UserId, forceRefresh: true) ?? string.Empty;
        else
            galBankCode = _bank.EnsureGalBankForEntity(actor);

        await RunOnMainThread(() =>
        {
            if (!Exists(atm) || !Exists(actor) || !_userInterface.HasUi(atm, BankATMMenuUiKey.Key))
                return;

            var enabled = _bank.TryGetBalance(actor, out var bankBalance);
            var balance = enabled ? bankBalance : 0;
            if (string.IsNullOrWhiteSpace(galBankCode))
                galBankCode = _bank.EnsureGalBankForEntity(actor);

            var history = GetOperationHistory(actor);
            var personalMessage = new LuaATMPersonalInfoMessage(enabled, balance, galBankCode, history);
            _userInterface.ServerSendUiMessage(atm, BankATMMenuUiKey.Key, personalMessage, actor);
        });
    }

    private async Task RunOnMainThread(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        });
        await tcs.Task;
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
        if (!_player.TryGetSessionByEntity(mobUid, out var session) ||
            !_bank.TryGetOperationHistory(session.UserId, out var history))
        {
            return [];
        }

        return history.Count > 10 ? [.. history.TakeLast(10)] : [.. history];
    }
}
