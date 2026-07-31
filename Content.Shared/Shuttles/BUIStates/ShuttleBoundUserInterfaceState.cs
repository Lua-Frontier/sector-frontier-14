using Content.Shared._Lua.Expedition;
using Content.Shared._Mono.FireControl; // Lua
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
    public bool FireControlConnected; // Lua
    public FireControllableEntry[]? FireControllables; // Lua
    public ExpeditionConsoleState? ExpeditionState;

    public ShuttleBoundUserInterfaceState(NavInterfaceState navState, ShuttleMapInterfaceState mapState, DockingInterfaceState dockState, StarmapConsoleBoundUserInterfaceState starMapState, bool fireControlConnected = false, FireControllableEntry[]? fireControllables = null, ExpeditionConsoleState? expeditionState = null)
    {
        NavState = navState;
        MapState = mapState;
        DockState = dockState;
        StarMapState = starMapState;
        FireControlConnected = fireControlConnected;
        FireControllables = fireControllables; ExpeditionState = expeditionState;
    }
}
