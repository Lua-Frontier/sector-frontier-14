// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Administration.Systems;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server._Lua.Administration;

public sealed class AdminArenaCleanupSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DeleteDelay = TimeSpan.FromMinutes(30);

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly AdminTestArenaSystem _arenaSystem = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextCheck;
    private readonly Dictionary<NetUserId, TimeSpan> _scheduledDeleteAt = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck) return;

        _nextCheck = _timing.CurTime + CheckInterval;
        RunCleanupPass();
    }

    private void RunCleanupPass()
    {
        if (_arenaSystem.ArenaMap.Count == 0)
        {
            _scheduledDeleteAt.Clear();
            return;
        }

        foreach (var (adminId, mapUid) in _arenaSystem.ArenaMap.ToArray())
        {
            if (Deleted(mapUid) || Terminating(mapUid))
            {
                ClearArena(adminId);
                continue;
            }

            if (!TryComp<MapComponent>(mapUid, out var mapComp))
            {
                ClearArena(adminId);
                continue;
            }

            var mapId = mapComp.MapId;
            var adminOnline = _playerManager.Sessions.Any(s => s.UserId == adminId && s.Status != SessionStatus.Disconnected);
            var hasOnlinePlayers = HasOnlinePlayersOnMap(mapId);

            var shouldScheduleDelete = !adminOnline || !hasOnlinePlayers;
            if (!shouldScheduleDelete)
            {
                _scheduledDeleteAt.Remove(adminId);
                continue;
            }

            if (!_scheduledDeleteAt.TryGetValue(adminId, out var deleteAt))
            {
                _scheduledDeleteAt[adminId] = _timing.CurTime + DeleteDelay;
                continue;
            }

            if (_timing.CurTime >= deleteAt)
                DeleteArena(adminId, mapId);
        }
    }

    private bool HasOnlinePlayersOnMap(MapId mapId)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.Disconnected) continue;
            if (session.AttachedEntity is not { } player)continue;
            if (Deleted(player) || Terminating(player)) continue;
            if (Transform(player).MapID == mapId) return true;
        }

        return false;
    }

    private void DeleteArena(NetUserId adminId, MapId mapId)
    {
        Log.Info($"Admin test arena removed for {adminId} mapId={mapId}");
        _mapSystem.DeleteMap(mapId);
        ClearArena(adminId);
    }

    private void ClearArena(NetUserId adminId)
    {
        _scheduledDeleteAt.Remove(adminId);
        _arenaSystem.ArenaMap.Remove(adminId);
        _arenaSystem.ArenaGrid.Remove(adminId);
    }
}
