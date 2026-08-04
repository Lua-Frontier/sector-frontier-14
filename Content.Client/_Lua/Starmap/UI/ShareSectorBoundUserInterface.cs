// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Starmap;
using Robust.Client.GameObjects;

namespace Content.Client._Lua.Starmap.UI;

public sealed class ShareSectorBoundUserInterface : BoundUserInterface
{
    private ShareSectorWindow? _window;

    public ShareSectorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new ShareSectorWindow();
        _window.OnShareSector += sectorId => SendMessage(new ShareSectorSelectedMessage(sectorId));
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ShareSectorBoundUserInterfaceState shareState)
            _window?.UpdateState(shareState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();
    }
}
