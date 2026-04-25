// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using Content.Shared._Lua.Stargate;
using Robust.Client.UserInterface;
namespace Content.Client._Lua.Stargate;
public sealed class StargateMinimapTabletBoundUserInterface : BoundUserInterface
{
    [ViewVariables] private StargateMinimapTabletWindow? _window;
    public StargateMinimapTabletBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }
    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StargateMinimapTabletWindow>();
        _window.OnClose += Close;
        _window.OnMarkerPlaced += (pos, label) => SendMessage(new StargateMinimapPlaceMarkerMessage(pos, label));
        _window.OnMarkerRemoved += idx => SendMessage(new StargateMinimapRemoveMarkerMessage(idx));
        _window.OnMergeDisk += (from, to) => SendMessage(new StargateMinimapMergeDiskMessage(from, to));
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is StargateMinimapUiState s) _window?.UpdateState(s);
    }
}
