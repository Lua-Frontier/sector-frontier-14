// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Shared.Map;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Serialization;
using Content.Shared.Timing;

namespace Content.Shared._Lua.Starmap;

public abstract class SharedStarmapSystem : EntitySystem
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
    public List<HyperlaneEdge> Edges;
    public float WarpCooldownRemainingSeconds;
    public float WarpCooldownTotalSeconds;
    public FTLState FTLState;
    public StartEndTime FTLTime;
    public List<MapId> VisibleSectorMaps;
    public Dictionary<MapId, string> SectorIdByMap;
    public Dictionary<MapId, string> OwnerByMap;
    public Dictionary<MapId, string> SectorColorOverrideHexByMap;

    public StarmapConsoleBoundUserInterfaceState(List<Star> stars, float range, List<HyperlaneEdge>? edges = null, float warpCooldownRemainingSeconds = 0f, float warpCooldownTotalSeconds = 0f, FTLState ftlState = FTLState.Invalid, StartEndTime ftlTime = default, List<MapId>? visibleSectorMaps = null, Dictionary<MapId, string>? sectorIdByMap = null, Dictionary<MapId, string>? ownerByMap = null, Dictionary<MapId, string>? sectorColorOverrideHexByMap = null)
    {
        Stars = stars;
        Range = range;
        Edges = edges ?? new List<HyperlaneEdge>();
        WarpCooldownRemainingSeconds = warpCooldownRemainingSeconds;
        WarpCooldownTotalSeconds = warpCooldownTotalSeconds;
        FTLState = ftlState;
        FTLTime = ftlTime;
        VisibleSectorMaps = visibleSectorMaps ?? new List<MapId>();
        SectorIdByMap = sectorIdByMap ?? new Dictionary<MapId, string>();
        OwnerByMap = ownerByMap ?? new Dictionary<MapId, string>();
        SectorColorOverrideHexByMap = sectorColorOverrideHexByMap ?? new Dictionary<MapId, string>();
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

[Serializable, NetSerializable]
public struct HyperlaneEdge
{
    public int A;
    public int B;

    public HyperlaneEdge(int a, int b)
    {
        A = a;
        B = b;
    }
}
