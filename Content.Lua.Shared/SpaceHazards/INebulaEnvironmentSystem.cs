using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.SpaceHazards;

public interface INebulaEnvironmentSystem : IEntitySystem
{
    float GetThrustMultiplier(EntityUid grid);
}
