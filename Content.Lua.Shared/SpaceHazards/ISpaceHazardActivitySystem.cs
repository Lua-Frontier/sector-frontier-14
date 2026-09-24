using System.Collections.Generic;
using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.SpaceHazards;

public interface ISpaceHazardActivitySystem : IEntitySystem
{
    IReadOnlyCollection<EntityUid> ActiveHazards { get; }
}
