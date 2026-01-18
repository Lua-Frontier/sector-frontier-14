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

    public CompanyMembersResponseEvent(string companyId, List<CompanyMemberEntry> members, bool viewerIsLeader, string viewerCompanyId)
    {
        CompanyId = companyId;
        Members = members;
        ViewerIsLeader = viewerIsLeader;
        ViewerCompanyId = viewerCompanyId;
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

