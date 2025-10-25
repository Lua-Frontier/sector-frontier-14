using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Shared.Serialization;
using Content.Shared._Lua.Starmap;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class ShuttleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public ShuttleMapInterfaceState MapState;
    public DockingInterfaceState DockState;
    public StarmapConsoleBoundUserInterfaceState StarMapState;

    public ShuttleBoundUserInterfaceState(NavInterfaceState navState, ShuttleMapInterfaceState mapState, DockingInterfaceState dockState, StarmapConsoleBoundUserInterfaceState starMapState)
    {
        NavState = navState;
        MapState = mapState;
        DockState = dockState;
        StarMapState = starMapState;
    }
}
