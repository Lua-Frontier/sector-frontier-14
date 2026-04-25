// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Stargate;
using Robust.Client.UserInterface;

namespace Content.Client._Lua.Stargate.UI;

public sealed class StarGateLandingBeaconBoundUserInterface : BoundUserInterface
{
    private StarGateLandingBeaconWindow? _window;

    public StarGateLandingBeaconBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StarGateLandingBeaconWindow>();
        _window.OnLandingPicked += (coords, angle) => SendMessage(new StarGateLandingBeaconFlyToMessage(coords, angle));
        _window.OnRecallPressed += () => SendMessage(new StarGateLandingBeaconRecallMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is StarGateLandingBeaconBoundUserInterfaceState s) _window?.UpdateState(s);
    }
}
