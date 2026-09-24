using Robust.Shared.Serialization;

namespace Content.Lua.Shared.Performance;

[Serializable, NetSerializable]
public sealed class ServerPerfUpdateEvent : EntityEventArgs
{
    public float ServerFpsAvg { get; init; }
    public ushort ServerTickRate { get; init; }
}


