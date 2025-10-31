// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared._Lua.Starmap;

namespace Content.Client._Lua.Starmap;

public sealed class SectorCaptureBoundUserInterface : BoundUserInterface
{
    private SectorCaptureWindow? _window;

    public SectorCaptureBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = new SectorCaptureWindow();
        _window.OnClose += Close;
        _window.OnStartCapture += () => SendMessage(new StartSectorCaptureBuiMsg());
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not SectorCaptureBuiState st) return;
        _window.Update(st);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _window?.Dispose();
    }
}


