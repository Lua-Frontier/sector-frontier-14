// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Client.UserInterface.Fragments;
using Content.Lua.Shared.GalBank.BUI;
using Robust.Client.UserInterface;

namespace Content.Client.Lua.GalBank.CartridgeLoader.Cartridges;

public sealed partial class GalBankTransferUi : UIFragment
{
    private GalBankTransferUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new GalBankTransferUiFragment();
        _fragment.Initialize(userInterface);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is GalBankTransferUiState cast)
            _fragment?.UpdateState(cast);
    }
}
