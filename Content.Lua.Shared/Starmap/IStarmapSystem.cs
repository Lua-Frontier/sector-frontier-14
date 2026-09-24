using System.Collections.Generic;
using Content.Lua.Shared.Starmap;
using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Starmap;

public interface IStarmapSystem : IEntitySystem
{
    List<Star> CollectStars();
    List<Star> CollectStarsFresh(bool updateCache);
    List<HyperlaneEdge> GetHyperlanesCached();
}
