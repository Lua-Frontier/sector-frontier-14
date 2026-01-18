using Content.Client._NF.Shipyard.UI;
using Content.Shared._Lua.Shipyard.Events;
using Content.Shared._Lua.Shipyard.BUIStates;
using Content.Shared._NF.Shipyard.BUI;
using Content.Shared._NF.Shipyard.Events;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._NF.Shipyard.BUI;

public sealed class ShipyardConsoleBoundUserInterface : BoundUserInterface
{
    private ShipyardConsoleMenu? _menu;
    // private ShipyardRulesPopup? _rulesWindow; // Frontier
    public int Balance { get; private set; }

    public int? ShipSellValue { get; private set; }

    public ShipyardConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        if (_menu == null)
        {
            _menu = this.CreateWindow<ShipyardConsoleMenu>();
            _menu.OnOrderApproved += ApproveOrder;
            _menu.OnSellShip += SellShip;
            _menu.OnUnassignDeed += UnassignDeed;
            _menu.OnRenameShip += RenameShip;
            _menu.OnDockPortSelected += SelectDockPort; // Lua
            _menu.TargetIdButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent("ShipyardConsole-targetId"));

            // Disable the NFSD popup for now.
            // var rules = new FormattedMessage();
            // _rulesWindow = new ShipyardRulesPopup(this);
            // if (ShipyardConsoleUiKey.Security == (ShipyardConsoleUiKey) UiKey)
            // {
            //     rules.AddText(Loc.GetString($"shipyard-rules-default1"));
            //     rules.PushNewline();
            //     rules.AddText(Loc.GetString($"shipyard-rules-default2"));
            //     _rulesWindow.ShipRules.SetMessage(rules);
            //     _rulesWindow.OpenCentered();
            // }
        }
    }

    private void Populate(List<string> availablePrototypes, List<string> unavailablePrototypes, bool freeListings, bool validId)
    {
        if (_menu == null)
            return;

        _menu.PopulateProducts(availablePrototypes, unavailablePrototypes, freeListings, validId);
        _menu.PopulateCategories(availablePrototypes, unavailablePrototypes);
        _menu.PopulateClasses(availablePrototypes, unavailablePrototypes);
        _menu.PopulateEngines(availablePrototypes, unavailablePrototypes);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        ShipyardConsoleInterfaceState? baseState = null;
        ShipyardConsoleLuaDockSelectState? dockState = null;

        if (state is ShipyardConsoleLuaDockSelectState lua)
        {
            baseState = lua.BaseState;
            dockState = lua;
        }
        else if (state is ShipyardConsoleInterfaceState plain) { baseState = plain; }
        else { return; }

        Balance = baseState.Balance;
        ShipSellValue = baseState.ShipSellValue;
        Populate(baseState.ShipyardPrototypes.available, baseState.ShipyardPrototypes.unavailable, baseState.FreeListings, baseState.IsTargetIdPresent);
        _menu?.UpdateState(baseState);
        if (dockState != null) _menu?.UpdateDockSelect(dockState.DockNavState, dockState.SelectedDockPort);
        else _menu?.UpdateDockSelect(null, null);
    }

    private void ApproveOrder(ButtonEventArgs args)
    {
        if (args.Button.Parent?.Parent is not VesselRow row || row.Vessel == null)
        {
            return;
        }

        var vesselId = row.Vessel.ID;
        SendMessage(new ShipyardConsolePurchaseMessage(vesselId));
    }

    private void SellShip(ButtonEventArgs args)
    {
        //reserved for a sanity check, but im not sure what since we check all the important stuffs on server already
        SendMessage(new ShipyardConsoleSellMessage());
    }

    private void UnassignDeed(ButtonEventArgs args)
    {
        SendMessage(new ShipyardConsoleUnassignDeedMessage());
    }

    private void RenameShip(string newName)
    {
        SendMessage(new ShipyardConsoleRenameMessage(newName));
    }

    private void SelectDockPort(NetEntity? port) // Lua
    { SendMessage(new SelectDockPortMessage(port)); }
}
