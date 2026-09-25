using Robust.Shared.Serialization;

namespace Content.Lua.Shared.ShipTracker;

public abstract class SharedShipTrackerSystem : EntitySystem
{
    [Serializable, NetSerializable]
    public enum ShieldGeneratorVisuals : byte
    {
        State
    }
}
