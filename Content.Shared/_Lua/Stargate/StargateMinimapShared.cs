// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using System.Numerics;
namespace Content.Shared._Lua.Stargate;
[Serializable, NetSerializable]
public enum StargateMinimapTabletUiKey : byte { Key }

[Serializable, NetSerializable]
public enum StargateMinimapTabletVisuals : byte
{
    HasDisk
}
public static class StargateMinimapConstants
{
    public const int ChunkSize = 16;
    public const int ChunkTileCount = ChunkSize * ChunkSize;
    public const int ExploreRadius = 7;
    public const int RefreshRadius = 5;
    public const uint WallColor = 0xFF99a3ad;
    public static uint PackColor(Color c) { return ((uint)c.AByte << 24) | ((uint)c.RByte << 16) | ((uint)c.GByte << 8) | c.BByte; }
    public static Color UnpackColor(uint p) { return new Color((byte)(p >> 16), (byte)(p >> 8), (byte)p, (byte)(p >> 24)); }
}
[Serializable, NetSerializable]
public sealed class StargateMinimapPlanetData
{
    public Dictionary<Vector2i, uint[]> Chunks = new();
    public List<StargateMinimapMarker> Markers = new();
}

[Serializable, NetSerializable]
public sealed class StargateMinimapMarker
{
    public Vector2 Position;
    public string? Label;
    public StargateMinimapMarker(Vector2 position, string? label) { Position = position; Label = label; }
}
[Serializable, NetSerializable]
public sealed class StargateMinimapUiState : BoundUserInterfaceState
{
    public readonly bool IsStargateWorld;
    public readonly bool HasDisk1;
    public readonly bool HasDisk2;
    public readonly Dictionary<Vector2i, uint[]> ExploredChunks;
    public readonly List<StargateMinimapMarker> Markers;
    public readonly Vector2? GatePosition;
    public readonly Vector2? PlayerPosition;
    public readonly List<Vector2> QuestTargetZones;
    public StargateMinimapUiState(bool isStargateWorld, bool hasDisk1, bool hasDisk2, Dictionary<Vector2i, uint[]> exploredChunks, List<StargateMinimapMarker> markers, Vector2? gatePosition, Vector2? playerPosition, List<Vector2>? questTargetZones = null) { IsStargateWorld = isStargateWorld; HasDisk1 = hasDisk1; HasDisk2 = hasDisk2; ExploredChunks = exploredChunks; Markers = markers; GatePosition = gatePosition; PlayerPosition = playerPosition; QuestTargetZones = questTargetZones ?? new(); }
}
[Serializable, NetSerializable]
public sealed class StargateMinimapPlaceMarkerMessage : BoundUserInterfaceMessage
{
    public readonly Vector2 Position;
    public readonly string? Label;
    public StargateMinimapPlaceMarkerMessage(Vector2 position, string? label) { Position = position; Label = label; }
}
[Serializable, NetSerializable]
public sealed class StargateMinimapRemoveMarkerMessage : BoundUserInterfaceMessage
{
    public readonly int Index;
    public StargateMinimapRemoveMarkerMessage(int index) { Index = index; }
}
[Serializable, NetSerializable]
public sealed class StargateMinimapMergeDiskMessage : BoundUserInterfaceMessage
{
    public readonly int FromSlot;
    public readonly int ToSlot;
    public StargateMinimapMergeDiskMessage(int fromSlot, int toSlot) { FromSlot = fromSlot; ToSlot = toSlot; }
}
