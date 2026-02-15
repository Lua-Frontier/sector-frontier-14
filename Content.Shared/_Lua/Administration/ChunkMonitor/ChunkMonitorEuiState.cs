// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Administration.ChunkMonitor;

[Serializable, NetSerializable]
public enum ChunkMonitorChunkStatus : byte
{
    Loaded = 0,
    Unloaded = 1,
}

[Serializable, NetSerializable]
public readonly record struct ChunkMonitorMapInfo(NetEntity MapUid, string Name);

[Serializable, NetSerializable]
public readonly record struct ChunkMonitorChunkInfo(
    Vector2i Coordinates,
    ChunkMonitorChunkStatus Status,
    int EntityCount,
    int PlayerCount,
    int ShuttleCount,
    int StationCount,
    int DebrisCount,
    int GridCount);

[Serializable, NetSerializable]
public sealed class ChunkMonitorEuiState : EuiStateBase
{
    public ChunkMonitorEuiState(
        NetEntity selectedMap,
        ChunkMonitorMapInfo[] maps,
        ChunkMonitorChunkInfo[] chunks,
        int loadedCount,
        int unloadedCount,
        int deletedCount)
    {
        SelectedMap = selectedMap;
        Maps = maps;
        Chunks = chunks;
        LoadedCount = loadedCount;
        UnloadedCount = unloadedCount;
        DeletedCount = deletedCount;
    }

    public NetEntity SelectedMap { get; }
    public ChunkMonitorMapInfo[] Maps { get; }
    public ChunkMonitorChunkInfo[] Chunks { get; }
    public int LoadedCount { get; }
    public int UnloadedCount { get; }
    public int DeletedCount { get; }
}

public static class ChunkMonitorEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class RequestMapData : EuiMessageBase
    {
        public RequestMapData(NetEntity mapUid)
        {
            MapUid = mapUid;
        }
        public NetEntity MapUid { get; }
    }

    [Serializable, NetSerializable]
    public sealed class DeleteChunks : EuiMessageBase
    {
        public DeleteChunks(NetEntity mapUid, Vector2i[] chunks)
        {
            MapUid = mapUid;
            Chunks = chunks;
        }
        public NetEntity MapUid { get; }
        public Vector2i[] Chunks { get; }
    }

    [Serializable, NetSerializable]
    public sealed class PurgeChunks : EuiMessageBase
    {
        public PurgeChunks(NetEntity mapUid, Vector2i[] chunks)
        {
            MapUid = mapUid;
            Chunks = chunks;
        }
        public NetEntity MapUid { get; }
        public Vector2i[] Chunks { get; }
    }
}
