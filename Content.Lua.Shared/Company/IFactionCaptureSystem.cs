using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Company;

public interface IFactionCaptureSystem : IEntitySystem
{
    void ResetCaptureState(EntityUid uid);
}
