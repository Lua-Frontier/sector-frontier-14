using Robust.Client.UserInterface;
using Content.Client.UserInterface.Fragments;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Events;
using Content.Client._Lua.CartridgeLoader.Cartridges; //Lua

namespace Content.Client._NF.CartridgeLoader.Cartridges;

/*
 Feature: UIFragment glue for YUPI cartridge.
 Wires: creates fragment, passes BUI to it, relays state updates (YupiTransferUiState).
*/
public sealed partial class YupiTransferUi : UIFragment
{
    private YupiTransferUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new YupiTransferUiFragment();
        _fragment.Initialize(userInterface);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is YupiTransferUiState cast)
            _fragment?.UpdateState(cast);
    }
}


