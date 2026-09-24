// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.EUI;
using Content.Shared.Database;
using Robust.Shared.Network;

namespace Content.Server.Administration;

public interface IReputationModerationEuiFactory
{
    BaseEui Create(ReputationTargetKind targetKind, NetUserId targetUserId, string targetName);
}
