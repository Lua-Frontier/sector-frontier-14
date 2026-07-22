// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client._Lua.Company.UI;
using Content.Shared._Lua.Company;
using Robust.Shared.Player;
using System.Numerics;

namespace Content.Client._Lua.Company;

public sealed class CompanyClientSystem : EntitySystem
{
    private CompanyFactionsWindow? _window;
    private CompanyCaptureWindow? _captureWindow;
    public event Action<int, IReadOnlyList<string>>? RejoinLocksUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CompanyMembersResponseEvent>(OnMembersResponse);
        SubscribeNetworkEvent<CompanyRejoinLocksResponseEvent>(OnRejoinLocksResponse);
        SubscribeNetworkEvent<CompanyMembersInvalidateEvent>(OnMembersInvalidate);
        SubscribeNetworkEvent<CompanyCaptureStatusEvent>(OnCaptureStatus);
        SubscribeNetworkEvent<CompanyInviteEvent>(OnInvitePrompt);
        SubscribeNetworkEvent<CompanyRevealRequestEvent>(OnRevealPrompt);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
    }

    public void RequestMembers(string companyId)
    { RaiseNetworkEvent(new CompanyMembersRequestEvent(companyId)); }

    public void RequestSetCompany(string companyId)
    { RaiseNetworkEvent(new CompanySetCompanyRequestEvent(companyId)); }

    public void RequestRejoinLocks(int characterSlot)
    { RaiseNetworkEvent(new CompanyRejoinLocksRequestEvent(characterSlot)); }

    public void RequestKick(string companyId, NetEntity target)
    { RaiseNetworkEvent(new CompanyKickRequestEvent(companyId, target)); }

    public void RequestDeclareWar(string targetCompanyId, string announcementText)
    { RaiseNetworkEvent(new CompanyDeclareWarRequestEvent(targetCompanyId, announcementText)); }

    public void RequestEndWar(int warId)
    { RaiseNetworkEvent(new CompanyEndWarRequestEvent(warId)); }

    public void RequestSetMotd(string companyId, string motd)
    { RaiseNetworkEvent(new CompanySetMotdRequestEvent(companyId, motd)); }

    public void RespondInvite(int inviteId, bool accept)
    { RaiseNetworkEvent(new CompanyInviteResponseEvent(inviteId, accept)); }

    public void RespondRevealRequest(int requestId, bool accept)
    { RaiseNetworkEvent(new CompanyRevealResponseEvent(requestId, accept)); }

    public void SetWindow(CompanyFactionsWindow? window)
    { _window = window; }

    private void OnMembersResponse(CompanyMembersResponseEvent ev)
    { _window?.UpdateMembers(ev.CompanyId, ev.Members, ev.ViewerIsLeader, ev.ViewerCompanyId, ev.Motd, ev.CanEditMotd, ev.WarState); }

    private void OnRejoinLocksResponse(CompanyRejoinLocksResponseEvent ev)
    { RejoinLocksUpdated?.Invoke(ev.CharacterSlot, ev.LockedCompanyIds); }

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

    private void OnCaptureStatus(CompanyCaptureStatusEvent ev)
    {
        if (!ev.Active)
        {
            CloseCaptureWindow();
            return;
        }

        if (_captureWindow == null || !_captureWindow.IsOpen)
        {
            _captureWindow = new CompanyCaptureWindow();
            _captureWindow.OpenCenteredAt(new Vector2(0.5f, 0.12f));
        }

        _captureWindow.UpdateState(ev);
    }

    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        CloseCaptureWindow();
    }

    private void OnRevealPrompt(CompanyRevealRequestEvent ev)
    {
        var prompt = new CompanyRevealRequestWindow(ev, this);
        prompt.OpenCentered();
    }

    private void CloseCaptureWindow()
    {
        _captureWindow?.Dispose();
        _captureWindow = null;
    }
}

