using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.FtlPoints;

public abstract class SharedFtlPointsSystem : EntitySystem
{ }

[NetSerializable, Serializable]
public enum StarmapConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class WarpToStarMessage : BoundUserInterfaceMessage
{
    public Star Star { get; }
    public WarpToStarMessage(Star star)
    { Star = star; }
}

[Serializable, NetSerializable]
public sealed class StarmapConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<Star> Stars;
    public float Range;

    public StarmapConsoleBoundUserInterfaceState(List<Star> stars, float range)
    {
        Stars = stars;
        Range = range;
    }
}

[Serializable, NetSerializable]
public struct Star
{
    public Vector2 Position;
    public Vector2 GlobalPosition;
    public string Name;
    public MapId Map;

    public Star(Vector2 position, MapId map, string name, Vector2 globalPosition)
    {
        Position = position;
        Name = name;
        Map = map;
        GlobalPosition = globalPosition;
    }
}
