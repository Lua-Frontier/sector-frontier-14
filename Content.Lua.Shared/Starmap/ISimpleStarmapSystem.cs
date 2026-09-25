using Content.Lua.Shared.Starmap;
using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Starmap;

public interface ISimpleStarmapSystem : IEntitySystem
{
    void WarpToStar(EntityUid consoleUid, Star star, EntityUid? actor = null);
}
