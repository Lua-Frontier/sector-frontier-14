using Content.Client._Lua.VendingMachines;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;

namespace Content.Client.VendingMachines;

public sealed class VendingMachineBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private LuaVendingMachineWindow? _menu;

    [ViewVariables]
    private List<VendingMachineInventoryEntry> _cachedInventory = new();

    [ViewVariables]
    private float _mod = 1f;
    [ViewVariables]
    private int _balance = 0;
    [ViewVariables]
    private int _cashSlotBalance = 0;

    public VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        if (EntMan.TryGetComponent<MarketModifierComponent>(Owner, out var market))
            _mod = market.Mod;
        _menu = this.CreateWindowCenteredLeft<LuaVendingMachineWindow>();
        if (EntMan.TryGetComponent(Owner, out MetaDataComponent? meta))
            _menu.Title = meta.EntityName;
        else
            _menu.Title = Loc.GetString("vending-machine-nf-fallback-title");
        _menu.OnItemSelected += OnItemSelected;
        Refresh();
    }

    public void Refresh()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineComponent? bendy) && !bendy.Ejecting;
        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);
        if (EntMan.TryGetComponent<BankAccountComponent>(PlayerManager.LocalEntity, out var bank))
            _balance = bank.Balance;
        else
            _balance = 0;
        int? cashSlotValue = null;
        if (TryUpdateCashSlotBalance())
            cashSlotValue = _cashSlotBalance;
        _menu?.Populate(_cachedInventory, enabled, _mod, _balance, cashSlotValue);
    }

    public void UpdateAmounts()
    {
        var enabled = EntMan.TryGetComponent(Owner, out VendingMachineComponent? bendy) && !bendy.Ejecting;
        if (EntMan.TryGetComponent<BankAccountComponent>(PlayerManager.LocalEntity, out var bank))
            _balance = bank.Balance;
        else
            _balance = 0;
        _menu?.UpdateBalance(_balance);
        if (TryUpdateCashSlotBalance())
            _menu?.UpdateCashSlotBalance(_cashSlotBalance);
        var system = EntMan.System<VendingMachineSystem>();
        _cachedInventory = system.GetAllInventory(Owner);
        _menu?.UpdateAmounts(_cachedInventory, _mod, enabled);
    }

    private void OnItemSelected(InventoryType type, string id)
    {
        SendPredictedMessage(new VendingMachineEjectMessage(type, id));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        if (_menu == null)
            return;
        _menu.OnItemSelected -= OnItemSelected;
        _menu.OnClose -= Close;
        _menu.Dispose();
    }

    public bool TryUpdateCashSlotBalance()
    {
        if (EntMan.TryGetComponent<VendingMachineComponent>(Owner, out var vendingMachine))
        {
            _cashSlotBalance = vendingMachine.CashSlotBalance;
            return true;
        }
        _cashSlotBalance = 0;
        return false;
    }
}
