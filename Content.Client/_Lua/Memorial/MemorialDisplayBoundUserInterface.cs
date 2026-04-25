// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Memorial;

namespace Content.Client._Lua.Memorial;

public sealed class MemorialDisplayBoundUserInterface : BoundUserInterface
{
    private MemorialDisplayWindow? _window;

    public MemorialDisplayBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new MemorialDisplayWindow();
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not MemorialDisplayUiState memorialState) return;
        _window?.UpdateState(memorialState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _window?.Dispose();
    }
}
