// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Company;

[Serializable, NetSerializable]
public sealed class CompanyMemberEntry
{
    public NetEntity Entity;
    public string Name;

    public CompanyMemberEntry(NetEntity entity, string name)
    {
        Entity = entity;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyMembersRequestEvent : EntityEventArgs
{
    public string CompanyId;

    public CompanyMembersRequestEvent(string companyId)
    {
        CompanyId = companyId;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyMembersResponseEvent : EntityEventArgs
{
    public string CompanyId;
    public List<CompanyMemberEntry> Members;
    public bool ViewerIsLeader;
    public string ViewerCompanyId;
    public string Motd;
    public bool CanEditMotd;
    public CompanyWarUiState? WarState;

    public CompanyMembersResponseEvent(string companyId, List<CompanyMemberEntry> members, bool viewerIsLeader, string viewerCompanyId, string motd = "", bool canEditMotd = false, CompanyWarUiState? warState = null)
    {
        CompanyId = companyId;
        Members = members;
        ViewerIsLeader = viewerIsLeader;
        ViewerCompanyId = viewerCompanyId;
        Motd = motd;
        CanEditMotd = canEditMotd;
        WarState = warState;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyWarOverview
{
    public int WarId;
    public string AggressorCompanyId;
    public string AggressorName;
    public string DefenderCompanyId;
    public string DefenderName;
    public string DeclaredBy;
    public string AnnouncementText;
    public float RemainingSeconds;
    public bool AggressorRequestedPeace;
    public bool DefenderRequestedPeace;

    public CompanyWarOverview(int warId, string aggressorCompanyId, string aggressorName, string defenderCompanyId, string defenderName, string declaredBy, string announcementText, float remainingSeconds, bool aggressorRequestedPeace, bool defenderRequestedPeace)
    {
        WarId = warId;
        AggressorCompanyId = aggressorCompanyId;
        AggressorName = aggressorName;
        DefenderCompanyId = defenderCompanyId;
        DefenderName = defenderName;
        DeclaredBy = declaredBy;
        AnnouncementText = announcementText;
        RemainingSeconds = remainingSeconds;
        AggressorRequestedPeace = aggressorRequestedPeace;
        DefenderRequestedPeace = defenderRequestedPeace;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyWarUiState
{
    public string ViewerCompanyId;
    public bool ViewerIsLeader;
    public bool CanDeclareWar;
    public bool CanEndWar;
    public string StatusText;
    public CompanyWarOverview? ActiveWar;
    public List<CompanyWarOverview> ActiveWars;

    public CompanyWarUiState(string viewerCompanyId, bool viewerIsLeader, bool canDeclareWar, bool canEndWar, string statusText, CompanyWarOverview? activeWar, List<CompanyWarOverview>? activeWars = null)
    {
        ViewerCompanyId = viewerCompanyId;
        ViewerIsLeader = viewerIsLeader;
        CanDeclareWar = canDeclareWar;
        CanEndWar = canEndWar;
        StatusText = statusText;
        ActiveWar = activeWar;
        ActiveWars = activeWars ?? new List<CompanyWarOverview>();
    }
}

[Serializable, NetSerializable]
public sealed class CompanyMembersInvalidateEvent : EntityEventArgs
{
    public string CompanyId;

    public CompanyMembersInvalidateEvent(string companyId)
    {
        CompanyId = companyId;
    }
}

[Serializable, NetSerializable]
public sealed class CompanySetCompanyRequestEvent : EntityEventArgs
{
    public string CompanyId;

    public CompanySetCompanyRequestEvent(string companyId)
    {
        CompanyId = companyId;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyRejoinLocksRequestEvent : EntityEventArgs
{
    public int CharacterSlot;

    public CompanyRejoinLocksRequestEvent(int characterSlot)
    {
        CharacterSlot = characterSlot;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyRejoinLocksResponseEvent : EntityEventArgs
{
    public int CharacterSlot;
    public List<string> LockedCompanyIds;

    public CompanyRejoinLocksResponseEvent(int characterSlot, List<string> lockedCompanyIds)
    {
        CharacterSlot = characterSlot;
        LockedCompanyIds = lockedCompanyIds;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyKickRequestEvent : EntityEventArgs
{
    public string CompanyId;
    public NetEntity Target;

    public CompanyKickRequestEvent(string companyId, NetEntity target)
    {
        CompanyId = companyId;
        Target = target;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyDeclareWarRequestEvent : EntityEventArgs
{
    public string TargetCompanyId;
    public string AnnouncementText;

    public CompanyDeclareWarRequestEvent(string targetCompanyId, string announcementText)
    {
        TargetCompanyId = targetCompanyId;
        AnnouncementText = announcementText;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyEndWarRequestEvent : EntityEventArgs
{
    public int WarId;

    public CompanyEndWarRequestEvent(int warId)
    {
        WarId = warId;
    }
}

[Serializable, NetSerializable]
public sealed class CompanySetMotdRequestEvent : EntityEventArgs
{
    public string CompanyId;
    public string Motd;

    public CompanySetMotdRequestEvent(string companyId, string motd)
    {
        CompanyId = companyId;
        Motd = motd;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyWarActionResult
{
    public bool Success;
    public string? Error;
    public string? Message;
    public int? WarId;

    public CompanyWarActionResult(bool success, string? error, string? message = null, int? warId = null)
    {
        Success = success;
        Error = error;
        Message = message;
        WarId = warId;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyCaptureStatusEvent : EntityEventArgs
{
    public bool Active;
    public string StationName;
    public string AttackerName;
    public string DefenderName;
    public float Progress;
    public int Attackers;
    public int RequiredAttackers;
    public int Defenders;
    public bool Paused;

    public CompanyCaptureStatusEvent(bool active, string stationName = "", string attackerName = "", string defenderName = "", float progress = 0f, int attackers = 0, int requiredAttackers = 0, int defenders = 0, bool paused = false)
    {
        Active = active;
        StationName = stationName;
        AttackerName = attackerName;
        DefenderName = defenderName;
        Progress = progress;
        Attackers = attackers;
        RequiredAttackers = requiredAttackers;
        Defenders = defenders;
        Paused = paused;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyInviteEvent : EntityEventArgs
{
    public int InviteId;
    public string InviterName;
    public string CompanyId;
    public string CompanyName;

    public CompanyInviteEvent(int inviteId, string inviterName, string companyId, string companyName)
    {
        InviteId = inviteId;
        InviterName = inviterName;
        CompanyId = companyId;
        CompanyName = companyName;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyInviteResponseEvent : EntityEventArgs
{
    public int InviteId;
    public bool Accept;

    public CompanyInviteResponseEvent(int inviteId, bool accept)
    {
        InviteId = inviteId;
        Accept = accept;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyRevealRequestEvent : EntityEventArgs
{
    public int RequestId;
    public string RequesterName;

    public CompanyRevealRequestEvent(int requestId, string requesterName)
    {
        RequestId = requestId;
        RequesterName = requesterName;
    }
}

[Serializable, NetSerializable]
public sealed class CompanyRevealResponseEvent : EntityEventArgs
{
    public int RequestId;
    public bool Accept;

    public CompanyRevealResponseEvent(int requestId, bool accept)
    {
        RequestId = requestId;
        Accept = accept;
    }
}

