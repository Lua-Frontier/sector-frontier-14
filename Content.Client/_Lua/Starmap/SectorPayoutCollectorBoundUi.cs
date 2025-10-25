// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared._Lua.Starmap;

namespace Content.Client._Lua.Starmap;

public sealed class PayoutCollectorBoundUserInterface : BoundUserInterface
{
    private PayoutCollectorWindow? _window;

    public PayoutCollectorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = new PayoutCollectorWindow();
        _window.OnClaim += () => SendMessage(new PayoutCollectorClaimMessage());
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    { if (_window == null || state is not PayoutCollectorBuiState st) return; _window.Update(st); }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return; _window?.Dispose();
    }
}


