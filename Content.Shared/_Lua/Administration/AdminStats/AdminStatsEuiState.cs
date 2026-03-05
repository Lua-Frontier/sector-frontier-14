// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaCorp
// See AGPLv3.txt for details.
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Administration.AdminStats;

[Serializable, NetSerializable]
public sealed class AdminStatsEuiState : EuiStateBase
{
    public int NpcActive;
    public int NpcSleeping;
    public int NpcTotal;

    public int ShuttlesActive;
    public int ShuttlesPaused;
    public int ShuttlesTotal;

    public int DebrisCount;
    public int WrecksCount;
    public int DebrisTotalCount;

    public int PlayersAlive;
    public int PlayersDead;
    public int PlayersInCryo;

    public int StargateMapsActive;
    public int StargateMapsFrozen;
    public int StargateMapsTotal;

    public long RamUsedBytes;
    public long RamTotalBytes;
    public double CpuPercent;
    public int CpuCount;
}

public static class AdminStatsEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class RefreshAllRequest : EuiMessageBase;

    [Serializable, NetSerializable]
    public sealed class RefreshResourcesRequest : EuiMessageBase;
}
