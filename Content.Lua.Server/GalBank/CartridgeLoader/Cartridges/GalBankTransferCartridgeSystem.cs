// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Threading.Tasks;
using Content.Lua.Server.Bank;
using Content.Server.CartridgeLoader;
using Content.Server.Popups;
using Content.Lua.Shared.GalBank.BUI;
using Content.Lua.Shared.GalBank.Events;
using Content.Shared.CartridgeLoader;
using Robust.Server.Containers;
using Robust.Shared.Asynchronous;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Lua.Server.GalBank.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class GalBankTransferCartridgeComponent : Component
{
}

public sealed class GalBankTransferCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GalBankTransferCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<GalBankTransferCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
    }

    private void OnUiReady(Entity<GalBankTransferCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        var loader = args.Loader;
        _ = RefreshUiAsync(loader);
    }

    private void OnUiMessage(Entity<GalBankTransferCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        var loader = GetEntity(args.LoaderUid);
        if (args is not GalBankTransferRequestMessage message)
            return;

        _ = HandleTransferAsync(loader, message.TargetCard, message.Amount);
    }

    private async Task RefreshUiAsync(EntityUid loader)
    {
        var owner = GetRootOwner(loader);

        if (_playerManager.TryGetSessionByEntity(owner, out var session))
        {
            _ = await _bank.FetchGalBankCodeAsync(session.UserId, forceRefresh: true);
        }
        else
        {
            _bank.EnsureGalBankForEntity(owner);
        }

        if (!Exists(loader))
            return;

        await RunOnMainThread(() =>
        {
            if (!Exists(loader))
                return;

            var code = string.Empty;
            var balance = 0;
            var currentOwner = GetRootOwner(loader);
            if (_playerManager.TryGetSessionByEntity(currentOwner, out var currentSession))
            {
                _bank.TryGetBalance(currentSession, out balance);
                code = _bank.GetCachedGalBankCode(currentSession.UserId);
                if (string.IsNullOrWhiteSpace(code))
                    code = _bank.EnsureGalBankForSessionSelected(currentSession);
            }
            else
            {
                _bank.TryGetBalance(loader, out balance);
                code = _bank.EnsureGalBankForEntity(currentOwner);
            }

            _cartridgeLoader.UpdateCartridgeUiState(loader, new GalBankTransferUiState(code, balance));
        });
    }

    private async Task HandleTransferAsync(EntityUid loader, string targetCard, int amount)
    {
        var (ok, error, newBal, recvAmount, toUserId) = await _bank.TryGalBankTransferAsync(loader, targetCard, amount);
        if (!Exists(loader))
            return;

        await RunOnMainThread(() =>
        {
            if (!Exists(loader))
                return;

            var owner = GetRootOwner(loader);
            if (ok)
            {
                _cartridgeLoader.UpdateCartridgeUiState(loader, new GalBankTransferUiState(GetCode(loader), newBal));
                _popup.PopupEntity(Loc.GetString("galbank-outgoing-transfer", ("code", GetCode(loader)), ("amount", recvAmount)), owner, owner);

                if (toUserId is { } guid &&
                    _playerManager.TryGetSessionById(new NetUserId(guid), out var targetSession) &&
                    targetSession.AttachedEntity is { Valid: true } target)
                {
                    _popup.PopupEntity(Loc.GetString("galbank-incoming-transfer", ("code", GetCode(loader)), ("amount", recvAmount)), target, target);
                }

                return;
            }

            var errText = error switch
            {
                BankSystem.GalBankTransferError.InvalidTarget => Loc.GetString("galbank-error-invalid-target"),
                BankSystem.GalBankTransferError.SelfTransfer => Loc.GetString("galbank-error-self-transfer"),
                BankSystem.GalBankTransferError.InvalidAmount => Loc.GetString("galbank-error-invalid-amount"),
                BankSystem.GalBankTransferError.Cooldown => Loc.GetString("galbank-error-cooldown"),
                BankSystem.GalBankTransferError.InsufficientFunds => Loc.GetString("bank-insufficient-funds"),
                BankSystem.GalBankTransferError.NetworkError => Loc.GetString("galbank-error-no-connection"),
                _ => Loc.GetString("bank-atm-menu-transaction-denied")
            };

            _cartridgeLoader.UpdateCartridgeUiState(loader, new GalBankTransferUiState(GetCode(loader), GetBalance(loader)));
            _popup.PopupEntity(errText, owner, owner);
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

    private string GetCode(EntityUid loader)
    {
        var owner = GetRootOwner(loader);
        if (_playerManager.TryGetSessionByEntity(owner, out var session))
        {
            var cached = _bank.GetCachedGalBankCode(session.UserId);
            if (!string.IsNullOrWhiteSpace(cached))
                return cached;
            return _bank.EnsureGalBankForSessionSelected(session);
        }

        return _bank.EnsureGalBankForEntity(owner);
    }

    private int GetBalance(EntityUid loader)
    {
        var owner = GetRootOwner(loader);
        if (_playerManager.TryGetSessionByEntity(owner, out var session))
        {
            _bank.TryGetBalance(session, out var balance);
            return balance;
        }

        _bank.TryGetBalance(loader, out var fb);
        return fb;
    }

    private EntityUid GetRootOwner(EntityUid ent)
    {
        var current = ent;
        while (_container.TryGetContainingContainer(current, out var cont))
            current = cont.Owner;
        return current;
    }
}
