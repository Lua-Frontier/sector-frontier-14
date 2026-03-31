// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.StationTeleporter;
using Robust.Client.UserInterface;

namespace Content.Client._Lua.StationTeleporter;

public sealed class StationTeleporterBoundUserInterface : BoundUserInterface
{
    private StationTeleporterWindow? _window;

    public StationTeleporterBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StationTeleporterWindow>();
        _window.SetOwner(Owner);
        _window.OnTeleporterClick += targetUid =>
        { SendMessage(new StationTeleporterClickMessage(targetUid)); };
        _window.OnRename += newName =>
        { SendMessage(new StationTeleporterRenameMessage(newName)); };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is StationTeleporterState s) _window?.UpdateState(s);
    }
}
