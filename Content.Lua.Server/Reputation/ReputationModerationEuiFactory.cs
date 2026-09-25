// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Database;
using Robust.Shared.Network;

namespace Content.Lua.Server.Reputation;

public sealed class ReputationModerationEuiFactory : IReputationModerationEuiFactory
{
    public BaseEui Create(ReputationTargetKind targetKind, NetUserId targetUserId, string targetName)
        => new ReputationModerationEui(targetKind, targetUserId, targetName);
}
