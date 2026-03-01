using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.Events;

[Serializable, NetSerializable]
public sealed class ShuttleConsoleFireMessage : BoundUserInterfaceMessage
{
    public List<NetEntity> Selected;
    public NetCoordinates Coordinates;

    public ShuttleConsoleFireMessage(List<NetEntity> selected, NetCoordinates coordinates)
    {
        Selected = selected;
        Coordinates = coordinates;
    }
}
[Serializable, NetSerializable]
public sealed class ShuttleConsoleRefreshFireControlMessage : BoundUserInterfaceMessage
{
}
