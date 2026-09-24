using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Shuttles;

public interface IMagneticLatchSystem : IEntitySystem
{
    void ShutdownLatch(EntityUid uid);
}
