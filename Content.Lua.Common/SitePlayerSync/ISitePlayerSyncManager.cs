// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Network;

namespace Content.Lua.Common.SitePlayerSync;

public interface ISitePlayerSyncManager
{
    void Initialize();
    void NotifyPlayerSeen(NetUserId userId, string userName);
}
