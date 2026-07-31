using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class DroneRouteState
{
    public NetEntity Steerer;
    public List<NetCoordinates> Points;

    public DroneRouteState(NetEntity steerer, List<NetCoordinates> points)
    {
        Steerer = steerer;
        Points = points;
    }
}
