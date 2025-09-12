using Content.Server.CartridgeLoader;
using Content.Server.Popups;
using Content.Server._NF.Bank;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._Lua.Bank.Events;
using Content.Shared.CartridgeLoader;
using Content.Shared._NF.Bank.Components;
using Robust.Shared.Player;
using Robust.Server.Containers;

namespace Content.Server._NF.CartridgeLoader.Cartridges;

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
            // Ensure code synchronously and read from component //Lua
            code = _bank.EnsureYupiForSessionSelected(session);
            _bank.TryGetBalance(session, out balance);
        }
        else
        {
            // Fallback: read balance/code directly from component //Lua
            _bank.TryGetBalance(loader, out balance);
            if (TryComp<BankAccountComponent>(owner, out var bank))
                code = bank.YupiCode;
        }

        // Finance integration (outstanding/due) remains as before; avoid async //Lua
        var outstanding = 0;
        var due = 0;
        if (playerMan.TryGetSessionByEntity(owner, out var s2))
        {
            var finance = EntityManager.System<Content.Server._NF.Finance.FinanceSystem>();
            var (d, _) = finance.GetDueAndHold(s2.UserId);
            due = d;
            outstanding = finance.GetOutstandingTotal(s2.UserId);
        }

        _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(code, balance, outstanding, due));
    }

    private void OnUiMessage(Entity<YupiTransferCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        var loader = GetEntity(args.LoaderUid);
        if (args is YupiTransferRequestMessage msg)
        {
            if (_bank.TryYupiTransfer(loader, msg.TargetCode, msg.Amount, out var error, out var newBal, out var recvAmount, out var recvCode))
            {
                var owner2 = GetRootOwner(loader);
                var playerMan2 = IoCManager.Resolve<ISharedPlayerManager>();
                var outstanding2 = 0;
                if (playerMan2.TryGetSessionByEntity(owner2, out var s3))
                {
                    var finance3 = EntityManager.System<Content.Server._NF.Finance.FinanceSystem>();
                    outstanding2 = finance3.GetOutstandingTotal(s3.UserId);
                }
                _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(GetCode(loader), newBal, outstanding2));
                // Outgoing transfer popup to sender (only sender sees it)
                var owner = GetRootOwner(loader);
                _popup.PopupEntity(Loc.GetString("yupi-outgoing-transfer", ("code", GetCode(loader)), ("amount", recvAmount)), owner, owner);
                if (_bank.TryResolveOnlineByYupiCode(msg.TargetCode, out var target, out _))
                    // Incoming popup visible only to the receiver
                    _popup.PopupEntity(Loc.GetString("yupi-incoming-transfer", ("code", GetCode(loader)), ("amount", recvAmount)), target, target);
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
            var playerManErr = IoCManager.Resolve<ISharedPlayerManager>();
            var outstandingErr = 0;
            if (playerManErr.TryGetSessionByEntity(ownerErr, out var sErr))
            {
                var financeErr = EntityManager.System<Content.Server._NF.Finance.FinanceSystem>();
                outstandingErr = financeErr.GetOutstandingTotal(sErr.UserId);
            }
            _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(GetCode(loader), GetBalance(loader), outstandingErr));
            // Error shown only to the sender
            _popup.PopupEntity(errText, GetRootOwner(loader), GetRootOwner(loader));
            return;
        }

        if (args is YupiRepayLoanRequestMessage repay)
        {
            var owner = GetRootOwner(loader);
            var playerMan = IoCManager.Resolve<ISharedPlayerManager>();
            if (!playerMan.TryGetSessionByEntity(owner, out var senderSession))
                return;

            var finance = EntityManager.System<Content.Server._NF.Finance.FinanceSystem>();
            var outstandingBefore = finance.GetOutstandingTotal(senderSession.UserId);
            var dueBefore = finance.GetDueAndHold(senderSession.UserId).due;
            var repayCap = outstandingBefore + dueBefore;
            if (repayCap <= 0)
            {
                _popup.PopupEntity(Loc.GetString("yupi-repay-nothing"), owner, owner);
                _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(GetCode(loader), GetBalance(loader), 0, 0));
                return;
            }
            var amount = Math.Min(repay.Amount, repayCap);
            if (amount <= 0)
            {
                _popup.PopupEntity(Loc.GetString("yupi-repay-nothing"), owner, owner);
                _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(GetCode(loader), GetBalance(loader), outstandingBefore, dueBefore));
                return;
            }

            // Withdraw from sender (repay own loans; ignore provided code)
            if (!_bank.TryBankWithdraw(owner, amount))
            {
                _popup.PopupEntity(Loc.GetString("bank-insufficient-funds"), owner, owner);
                _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(GetCode(loader), GetBalance(loader), outstandingBefore, dueBefore));
                return;
            }

            var paidToDue = finance.PayDue(senderSession.UserId, amount);
            var remaining = Math.Max(0, amount - paidToDue);
            if (remaining > 0)
                finance.TryEvenRepayLoans(senderSession.UserId, remaining);

            var (d3, _) = finance.GetDueAndHold(senderSession.UserId);
            var o3 = finance.GetOutstandingTotal(senderSession.UserId);
            _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiTransferUiState(GetCode(loader), GetBalance(loader), o3, d3));
            _popup.PopupEntity(Loc.GetString("yupi-repay-success", ("amount", amount)), owner, owner);
        }
    }

    private string GetCode(EntityUid loader)
    {
        var owner = GetRootOwner(loader);
        //Lua: read from BankAccountComponent; no preferences access
        if (TryComp<BankAccountComponent>(owner, out var bank))
            return bank.YupiCode;
        return string.Empty;
    }

    private int GetBalance(EntityUid loader)
    {
        var playerMan = IoCManager.Resolve<ISharedPlayerManager>();
        var owner = GetRootOwner(loader);
        if (playerMan.TryGetSessionByEntity(owner, out var session))
        {
            _bank.TryGetBalance(session, out var bal);
            return bal;
        }
        _bank.TryGetBalance(loader, out var fb);
        return fb;
    }

    private EntityUid GetRootOwner(EntityUid ent)
    {
        var current = ent;
        while (_container.TryGetContainingContainer(current, out var cont))
        {
            current = cont.Owner;
        }
        return current;
    }
}
