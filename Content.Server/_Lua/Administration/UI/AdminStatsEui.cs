// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using Content.Server._Lua.Shipyard.Components;
using Content.Server._Lua.Stargate.Components;
using Content.Server._NF.CryoSleep;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
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
using System.IO;
using System.Runtime.InteropServices;

namespace Content.Server._Lua.Administration.UI;

public sealed class AdminStatsEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    private readonly bool _isLinux;
    private readonly bool _isWindows;
    private long _prevHostCpuIdle;
    private long _prevHostCpuTotal;
    private long _prevWinIdleTicks;
    private long _prevWinKernelTicks;
    private long _prevWinUserTicks;
    private readonly AdminStatsEuiState _cachedState = new();

    public AdminStatsEui()
    {
        IoCManager.InjectDependencies(this);
        _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (_isLinux) ReadLinuxCpuTotals(out _prevHostCpuIdle, out _prevHostCpuTotal);
        else if (_isWindows) GetSystemTimes(out _prevWinIdleTicks, out _prevWinKernelTicks, out _prevWinUserTicks);
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
            IsLinuxHost = _cachedState.IsLinuxHost,
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
        var htnQuery = _entMan.AllEntityQueryEnumerator<HTNComponent>();
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
        var query = _entMan.AllEntityQueryEnumerator<ShuttleComponent, ShuttleDeedComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out _, out _, out _))
        {
            _cachedState.ShuttlesTotal++;
            if (_entMan.HasComponent<ParkedShuttleComponent>(uid)) _cachedState.ShuttlesPaused++;
            else _cachedState.ShuttlesActive++;
        }
    }

    private void CollectDebrisStats()
    {
        _cachedState.DebrisCount = 0;
        _cachedState.WrecksCount = 0;
        var query = _entMan.AllEntityQueryEnumerator<MapGridComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out _, out var meta))
        {
            var name = meta.EntityName;
            if (name.Contains("[Астероид]")) _cachedState.DebrisCount++;
            else if (name.Contains("[Обломок]")) _cachedState.WrecksCount++;
        }
        _cachedState.DebrisTotalCount = _cachedState.DebrisCount + _cachedState.WrecksCount;
    }

    private void CollectPlayerStats()
    {
        _cachedState.PlayersAlive = 0;
        _cachedState.PlayersDead = 0;
        _cachedState.PlayersInCryo = 0;
        var aliveQuery = _entMan.AllEntityQueryEnumerator<ActorComponent, MobStateComponent>();
        while (aliveQuery.MoveNext(out _, out _, out var aliveMob))
        { if (aliveMob.CurrentState is MobState.Alive or MobState.Critical) _cachedState.PlayersAlive++; }
        var deadQuery = _entMan.AllEntityQueryEnumerator<MindContainerComponent, MobStateComponent>();
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
        var query = _entMan.AllEntityQueryEnumerator<StargateDestinationComponent>();
        while (query.MoveNext(out _, out var dest))
        {
            _cachedState.StargateMapsTotal++;
            if (dest.Frozen) _cachedState.StargateMapsFrozen++;
            else _cachedState.StargateMapsActive++;
        }
    }

    private void CollectResourceStats()
    {
        _cachedState.IsLinuxHost = _isLinux || _isWindows;
        _cachedState.CpuCount = Environment.ProcessorCount;
        if (_isLinux)
        {
            CollectLinuxRam();
            CollectLinuxCpu();
        }
        else if (_isWindows)
        {
            CollectWindowsRam();
            CollectWindowsCpu();
        }
        else
        {
            _cachedState.RamUsedBytes = 0;
            _cachedState.RamTotalBytes = 0;
            _cachedState.CpuPercent = double.NaN;
        }
    }

    private void CollectLinuxRam()
    {
        long totalKb = 0;
        long availableKb = 0;
        try
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:")) totalKb = ParseMemInfoKb(line);
                else if (line.StartsWith("MemAvailable:")) availableKb = ParseMemInfoKb(line);
            }
        }
        catch { }
        _cachedState.RamTotalBytes = totalKb * 1024;
        _cachedState.RamUsedBytes = (totalKb - availableKb) * 1024;
    }
    private static long ParseMemInfoKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], out var kb) ? kb : 0;
    }
    private void CollectLinuxCpu()
    {
        if (!ReadLinuxCpuTotals(out var idle, out var total)) return;
        var deltaTotal = total - _prevHostCpuTotal;
        var deltaIdle = idle - _prevHostCpuIdle;
        _cachedState.CpuPercent = deltaTotal > 0 ? (1.0 - (double) deltaIdle / deltaTotal) * 100.0 : 0;
        _prevHostCpuIdle = idle;
        _prevHostCpuTotal = total;
    }

    private static bool ReadLinuxCpuTotals(out long idle, out long total)
    {
        idle = 0;
        total = 0;
        try
        {
            using var sr = new StreamReader("/proc/stat");
            var cpuLine = sr.ReadLine();
            if (cpuLine == null || !cpuLine.StartsWith("cpu ")) return false;
            var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) return false;
            for (var i = 1; i < parts.Length; i++)
            { if (long.TryParse(parts[i], out var v)) total += v; }
            if (long.TryParse(parts[4], out var idleVal)) idle = idleVal;
            return true;
        }
        catch { return false; }
    }

    #region Windows

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);
    private void CollectWindowsRam()
    {
        var memStatus = new MemoryStatusEx { Length = (uint) Marshal.SizeOf<MemoryStatusEx>() };
        if (GlobalMemoryStatusEx(ref memStatus))
        {
            _cachedState.RamTotalBytes = (long) memStatus.TotalPhys;
            _cachedState.RamUsedBytes = (long) (memStatus.TotalPhys - memStatus.AvailPhys);
        }
    }

    private void CollectWindowsCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return;
        var deltaIdle = idle - _prevWinIdleTicks;
        var deltaKernel = kernel - _prevWinKernelTicks;
        var deltaUser = user - _prevWinUserTicks;
        var deltaTotal = deltaKernel + deltaUser;
        _cachedState.CpuPercent = deltaTotal > 0 ? (1.0 - (double) deltaIdle / deltaTotal) * 100.0 : 0;
        _prevWinIdleTicks = idle;
        _prevWinKernelTicks = kernel;
        _prevWinUserTicks = user;
    }

    #endregion

    private bool EnsureAuthorized()
    {
        if (_admins.HasAdminFlag(Player, AdminFlags.Admin)) return true;
        Close();
        return false;
    }
}
