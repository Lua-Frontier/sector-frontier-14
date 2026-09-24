using Content.Lua.Shared.Shuttles.Components;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Lua.Shared.Shuttles;

public interface IShuttleTabletSystem : IEntitySystem
{
    void UpdateTabletState(EntityUid tablet, ShuttleTabletComponent tabletComp, DockingInterfaceState? dockState = null);
    bool IsValidTablet(EntityUid tablet, ShuttleTabletComponent tabletComp, out float linkPower);
    EntityCoordinates? GetTabletCoordinates(EntityUid tablet);
    EntityUid? GetTabletGrid(EntityUid? tablet);
}
