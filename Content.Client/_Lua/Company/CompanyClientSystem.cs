// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client._Lua.Company.UI;
using Content.Shared._Lua.Company;

namespace Content.Client._Lua.Company;

public sealed class CompanyClientSystem : EntitySystem
{
    private CompanyFactionsWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CompanyMembersResponseEvent>(OnMembersResponse);
        SubscribeNetworkEvent<CompanyMembersInvalidateEvent>(OnMembersInvalidate);
        SubscribeNetworkEvent<CompanyInviteEvent>(OnInvitePrompt);
    }

    public void RequestMembers(string companyId)
    { RaiseNetworkEvent(new CompanyMembersRequestEvent(companyId)); }

    public void RequestSetCompany(string companyId)
    { RaiseNetworkEvent(new CompanySetCompanyRequestEvent(companyId)); }

    public void RequestKick(string companyId, NetEntity target)
    { RaiseNetworkEvent(new CompanyKickRequestEvent(companyId, target)); }

    public void RespondInvite(int inviteId, bool accept)
    { RaiseNetworkEvent(new CompanyInviteResponseEvent(inviteId, accept)); }

    public void SetWindow(CompanyFactionsWindow? window)
    { _window = window; }

    private void OnMembersResponse(CompanyMembersResponseEvent ev)
    { _window?.UpdateMembers(ev.CompanyId, ev.Members, ev.ViewerIsLeader, ev.ViewerCompanyId); }

    private void OnMembersInvalidate(CompanyMembersInvalidateEvent ev)
    {
        if (_window == null || !_window.IsOpen) return;
        var selected = _window.SelectedCompanyId;
        if (selected == null) return;
        if (!string.Equals(selected, ev.CompanyId, StringComparison.OrdinalIgnoreCase)) return;
        RequestMembers(ev.CompanyId);
    }

    private void OnInvitePrompt(CompanyInviteEvent ev)
    {
        var prompt = new CompanyInviteWindow(ev, this);
        prompt.OpenCentered();
    }
}

