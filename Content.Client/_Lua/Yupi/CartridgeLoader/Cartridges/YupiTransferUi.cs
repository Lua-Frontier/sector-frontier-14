/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Robust.Client.UserInterface;
using Content.Client.UserInterface.Fragments;
using Content.Shared._NF.Bank.BUI;

namespace Content.Client._Lua.Yupi.CartridgeLoader.Cartridges;

public sealed partial class YupiTransferUi : UIFragment
{
    private YupiTransferUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    { return _fragment!; }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new YupiTransferUiFragment();
        _fragment.Initialize(userInterface);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    { if (state is YupiTransferUiState cast) _fragment?.UpdateState(cast); }
}


