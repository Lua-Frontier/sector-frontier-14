using Content.Server.CartridgeLoader;
using Content.Server.Popups;
using Content.Server._NF.Bank;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._Lua.Bank.Events;
using Content.Shared.CartridgeLoader;
using Content.Shared._NF.Bank.Components;
using Robust.Shared.Player;
using Robust.Server.Containers;

namespace Content.Server._Lua.Yupi.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class YupiTransferCartridgeComponent : Component { }

public sealed class YupiTransferCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<YupiTransferCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<YupiTransferCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
    }

    private void OnUiReady(Entity<YupiTransferCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        var loader = args.Loader;
        var code = string.Empty;
        var balance = 0;
        var owner = GetRootOwner(loader);
        var playerMan = IoCManager.Resolve<ISharedPlayerManager>();
        if (playerMan.TryGetSessionByEntity(owner, out var session))
        {
            code = _bank.EnsureYupiForSessionSelected(session);
            _bank.TryGetBalance(session, out balance);
        }
        else
        {
            _bank.TryGetBalance(loader, out balance);
            if (TryComp<BankAccountComponent>(owner, out var bank)) code = bank.YupiCode;
        }
        _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(code, balance));
    }

    private void OnUiMessage(Entity<YupiTransferCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        var loader = GetEntity(args.LoaderUid);
        if (args is YupiTransferRequestMessage message)
        {
            if (_bank.TryYupiTransfer(loader, message.TargetCode, message.Amount, out var error, out var newBal, out var recvAmount, out _))
            {
                _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(GetCode(loader), newBal));
                var owner = GetRootOwner(loader);
                _popup.PopupEntity(Loc.GetString("yupi-outgoing-transfer", ("code", GetCode(loader)), ("amount", recvAmount)), owner, owner);
                if (_bank.TryResolveOnlineByYupiCode(message.TargetCode, out var target)) _popup.PopupEntity(Loc.GetString("yupi-incoming-transfer", ("code", GetCode(loader)), ("amount", recvAmount)), target, target);
                return;
            }

            var errText = error switch
            {
                BankSystem.YupiTransferError.InvalidTarget => Loc.GetString("yupi-error-invalid-target"),
                BankSystem.YupiTransferError.SelfTransfer => Loc.GetString("yupi-error-self-transfer"),
                BankSystem.YupiTransferError.InvalidAmount => Loc.GetString("yupi-error-invalid-amount"),
                BankSystem.YupiTransferError.ExceedsPerTransferLimit => Loc.GetString("yupi-error-over-50k"),
                BankSystem.YupiTransferError.InsufficientFunds => Loc.GetString("bank-insufficient-funds"),
                BankSystem.YupiTransferError.ExceedsWindowLimit => Loc.GetString("yupi-error-window-limit"),
                _ => Loc.GetString("bank-atm-menu-transaction-denied")
            };
            var ownerErr = GetRootOwner(loader);
            _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(GetCode(loader), GetBalance(loader)));
            _popup.PopupEntity(errText, GetRootOwner(loader), GetRootOwner(loader));
            return;
        }
    }

    private string GetCode(EntityUid loader)
    {
        var owner = GetRootOwner(loader);
        if (TryComp<BankAccountComponent>(owner, out var bank)) return bank.YupiCode;
        return string.Empty;
    }

    private int GetBalance(EntityUid loader)
    {
        var playerMan = IoCManager.Resolve<ISharedPlayerManager>();
        var owner = GetRootOwner(loader);
        if (playerMan.TryGetSessionByEntity(owner, out var session))
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
        { current = cont.Owner; }
        return current;
    }
}
