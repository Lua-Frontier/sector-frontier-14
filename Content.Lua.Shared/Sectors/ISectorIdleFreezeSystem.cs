using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Lua.Shared.Sectors;

public interface ISectorIdleFreezeSystem : IEntitySystem
{
    void PinForRule(EntityUid rule, MapId mapId);
    void UnpinForRule(EntityUid rule);
    void EnsureUnfrozen(MapId mapId);
}
