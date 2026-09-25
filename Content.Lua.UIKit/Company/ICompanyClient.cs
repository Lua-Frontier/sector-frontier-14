// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameObjects;

namespace Content.Lua.UIKit.Company;

public interface ICompanyClient : IEntitySystem
{
    event Action<int, IReadOnlyList<string>>? RejoinLocksUpdated;
    void RequestMembers(string companyId);
    void RequestSetCompany(string companyId);
    void RequestRejoinLocks(int characterSlot);
    void RequestKick(string companyId, NetEntity target);
    void RequestDeclareWar(string targetCompanyId, string announcementText);
    void RequestEndWar(int warId);
    void RequestSetMotd(string companyId, string motd);
    void RespondInvite(int inviteId, bool accept);
    void RespondRevealRequest(int requestId, bool accept);
}
