// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Stargate;
using Robust.Client.UserInterface;

namespace Content.Client._Lua.Stargate;

public sealed class StargateAddressEditorBoundUserInterface : BoundUserInterface
{
    private StargateAddressEditorWindow? _window;

    public StargateAddressEditorBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StargateAddressEditorWindow>();

        _window.OnSymbolPressed += symbol =>
        {
            SendMessage(new StargateAddressEditorInputSymbolMessage(symbol));
        };

        _window.OnClear += () =>
        {
            SendMessage(new StargateAddressEditorClearInputMessage());
        };

        _window.OnSaveToLeft += () =>
        {
            SendMessage(new StargateAddressEditorSaveToLeftMessage());
        };

        _window.OnSaveToRight += () =>
        {
            SendMessage(new StargateAddressEditorSaveToRightMessage());
        };

        _window.OnDeleteFromLeft += index =>
        {
            SendMessage(new StargateAddressEditorDeleteFromLeftMessage(index));
        };

        _window.OnDeleteFromRight += index =>
        {
            SendMessage(new StargateAddressEditorDeleteFromRightMessage(index));
        };

        _window.OnMoveLeftToRight += index =>
        {
            SendMessage(new StargateAddressEditorMoveLeftToRightMessage(index));
        };

        _window.OnMoveRightToLeft += index =>
        {
            SendMessage(new StargateAddressEditorMoveRightToLeftMessage(index));
        };

        _window.OnCopyLeftToRight += index =>
        {
            SendMessage(new StargateAddressEditorCopyLeftToRightMessage(index));
        };

        _window.OnCopyRightToLeft += index =>
        {
            SendMessage(new StargateAddressEditorCopyRightToLeftMessage(index));
        };

        _window.OnCloneLeftToRight += () =>
        {
            SendMessage(new StargateAddressEditorCloneLeftToRightMessage());
        };

        _window.OnCloneRightToLeft += () =>
        {
            SendMessage(new StargateAddressEditorCloneRightToLeftMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is StargateAddressEditorUiState s)
            _window?.UpdateState(s);
    }
}
