using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.ShipProtection;

public interface IShipProtectionSystem : IEntitySystem
{
    void ProtectEntity(EntityUid uid, TimeSpan duration);
    int GetRemainingMinutes(EntityUid uid);
}
