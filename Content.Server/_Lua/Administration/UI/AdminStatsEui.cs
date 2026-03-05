// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using Content.Server._Lua.Shuttles.Components;
using Content.Server._Lua.Stargate.Components;
using Content.Server._NF.CryoSleep;
using Content.Server._NF.Worldgen.Components.Debris;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
using Content.Server.Worldgen.Components.GC;
using Content.Shared._Lua.Administration.AdminStats;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using System.Diagnostics;

namespace Content.Server._Lua.Administration.UI;

public sealed class AdminStatsEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    private TimeSpan _lastCpuTime;
    private DateTime _lastCpuCheck;
    private readonly AdminStatsEuiState _cachedState = new();

    public AdminStatsEui()
    {
        IoCManager.InjectDependencies(this);
        var proc = Process.GetCurrentProcess();
        _lastCpuTime = proc.TotalProcessorTime;
        _lastCpuCheck = DateTime.UtcNow;
    }

    public override void Opened()
    {
        base.Opened();
        if (!EnsureAuthorized()) return;
        CollectAll();
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (!EnsureAuthorized()) return;
        switch (msg)
        {
            case AdminStatsEuiMsg.RefreshAllRequest:
                CollectAll();
                StateDirty();
                break;
            case AdminStatsEuiMsg.RefreshResourcesRequest:
                CollectResourceStats();
                StateDirty();
                break;
        }
    }

    public override EuiStateBase GetNewState()
    {
        return new AdminStatsEuiState
        {
            NpcActive = _cachedState.NpcActive,
            NpcSleeping = _cachedState.NpcSleeping,
            NpcTotal = _cachedState.NpcTotal,
            ShuttlesActive = _cachedState.ShuttlesActive,
            ShuttlesPaused = _cachedState.ShuttlesPaused,
            ShuttlesTotal = _cachedState.ShuttlesTotal,
            DebrisCount = _cachedState.DebrisCount,
            WrecksCount = _cachedState.WrecksCount,
            DebrisTotalCount = _cachedState.DebrisTotalCount,
            PlayersAlive = _cachedState.PlayersAlive,
            PlayersDead = _cachedState.PlayersDead,
            PlayersInCryo = _cachedState.PlayersInCryo,
            StargateMapsActive = _cachedState.StargateMapsActive,
            StargateMapsFrozen = _cachedState.StargateMapsFrozen,
            StargateMapsTotal = _cachedState.StargateMapsTotal,
            RamUsedBytes = _cachedState.RamUsedBytes,
            RamTotalBytes = _cachedState.RamTotalBytes,
            CpuPercent = _cachedState.CpuPercent,
            CpuCount = _cachedState.CpuCount,
        };
    }

    private void CollectAll()
    {
        CollectNpcStats();
        CollectShuttleStats();
        CollectDebrisStats();
        CollectPlayerStats();
        CollectStargateStats();
        CollectResourceStats();
    }

    private void CollectNpcStats()
    {
        _cachedState.NpcActive = 0;
        _cachedState.NpcSleeping = 0;
        _cachedState.NpcTotal = 0;
        var htnQuery = _entMan.EntityQueryEnumerator<HTNComponent>();
        while (htnQuery.MoveNext(out var uid, out _))
        {
            _cachedState.NpcTotal++;
            if (_entMan.HasComponent<ActiveNPCComponent>(uid)) _cachedState.NpcActive++;
            else _cachedState.NpcSleeping++;
        }
    }

    private void CollectShuttleStats()
    {
        _cachedState.ShuttlesActive = 0;
        _cachedState.ShuttlesPaused = 0;
        _cachedState.ShuttlesTotal = 0;
        var query = _entMan.EntityQueryEnumerator<ShuttleComponent, ShuttleDeedComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out _, out _, out _))
        {
            _cachedState.ShuttlesTotal++;
            if (_entMan.TryGetComponent<ShuttleFreezeStateComponent>(uid, out var freeze) && freeze.Frozen) _cachedState.ShuttlesPaused++;
            else _cachedState.ShuttlesActive++;
        }
    }

    private void CollectDebrisStats()
    {
        _cachedState.DebrisCount = 0;
        _cachedState.WrecksCount = 0;
        var debrisQuery = _entMan.EntityQueryEnumerator<SpaceDebrisComponent>();
        while (debrisQuery.MoveNext(out _, out _)) _cachedState.DebrisCount++;
        var gcQuery = _entMan.EntityQueryEnumerator<GCAbleObjectComponent>();
        while (gcQuery.MoveNext(out var uid, out _))
        { if (!_entMan.HasComponent<SpaceDebrisComponent>(uid)) _cachedState.WrecksCount++; }
        _cachedState.DebrisTotalCount = _cachedState.DebrisCount + _cachedState.WrecksCount;
    }

    private void CollectPlayerStats()
    {
        _cachedState.PlayersAlive = 0;
        _cachedState.PlayersDead = 0;
        _cachedState.PlayersInCryo = 0;
        var aliveQuery = _entMan.EntityQueryEnumerator<ActorComponent, MobStateComponent>();
        while (aliveQuery.MoveNext(out _, out _, out var aliveMob))
        { if (aliveMob.CurrentState is MobState.Alive or MobState.Critical) _cachedState.PlayersAlive++; }
        var deadQuery = _entMan.EntityQueryEnumerator<MindContainerComponent, MobStateComponent>();
        while (deadQuery.MoveNext(out _, out _, out var deadMob))
        { if (deadMob.CurrentState == MobState.Dead) _cachedState.PlayersDead++; }
        var cryoSystem = _entMan.System<CryoSleepSystem>();
        _cachedState.PlayersInCryo = cryoSystem.GetCryosleepingCount();
    }

    private void CollectStargateStats()
    {
        _cachedState.StargateMapsActive = 0;
        _cachedState.StargateMapsFrozen = 0;
        _cachedState.StargateMapsTotal = 0;
        var query = _entMan.EntityQueryEnumerator<StargateDestinationComponent>();
        while (query.MoveNext(out _, out var dest))
        {
            _cachedState.StargateMapsTotal++;
            if (dest.Frozen) _cachedState.StargateMapsFrozen++;
            else _cachedState.StargateMapsActive++;
        }
    }

    private void CollectResourceStats()
    {
        var proc = Process.GetCurrentProcess();
        _cachedState.RamUsedBytes = proc.WorkingSet64;
        var gcInfo = GC.GetGCMemoryInfo();
        _cachedState.RamTotalBytes = gcInfo.TotalAvailableMemoryBytes;
        var now = DateTime.UtcNow;
        var cpuTime = proc.TotalProcessorTime;
        var elapsed = (now - _lastCpuCheck).TotalMilliseconds;
        if (elapsed > 0)
        {
            var cpuUsed = (cpuTime - _lastCpuTime).TotalMilliseconds;
            _cachedState.CpuPercent = cpuUsed / elapsed / Environment.ProcessorCount * 100.0;
        }
        _lastCpuTime = cpuTime;
        _lastCpuCheck = now;
        _cachedState.CpuCount = Environment.ProcessorCount;
    }

    private bool EnsureAuthorized()
    {
        if (_admins.HasAdminFlag(Player, AdminFlags.Admin)) return true;
        Close();
        return false;
    }
}
