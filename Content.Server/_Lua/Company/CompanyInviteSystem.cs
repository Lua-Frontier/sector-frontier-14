// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Company;
using Content.Shared._Mono.Company;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Verbs;
using Content.Shared.Roles.Jobs;
using Content.Shared.Players;
using Content.Server._Mono.Company;
using Content.Server.Preferences.Managers;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Threading;

namespace Content.Server._Lua.Company;

public sealed class CompanyInviteSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPlayerSystem _playerSystem = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly CompanySystem _companySystem = default!;
    [Dependency] private readonly FactionWarSystem _factionWar = default!;
    [Dependency] private readonly CompanyMotdSystem _motds = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;

    private int _nextInviteId = 1;
    private readonly Dictionary<int, PendingInvite> _pendingInvites = new();
    private int _nextRevealRequestId = 1;
    private readonly Dictionary<int, PendingRevealRequest> _pendingRevealRequests = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CompanyMembersRequestEvent>(OnMembersRequest);
        SubscribeNetworkEvent<CompanySetCompanyRequestEvent>(OnSetCompanyRequest);
        SubscribeNetworkEvent<CompanyKickRequestEvent>(OnKickRequest);
        SubscribeNetworkEvent<CompanyDeclareWarRequestEvent>(OnDeclareWarRequest);
        SubscribeNetworkEvent<CompanyEndWarRequestEvent>(OnEndWarRequest);
        SubscribeNetworkEvent<CompanyInviteResponseEvent>(OnInviteResponse);
        SubscribeNetworkEvent<CompanyRevealResponseEvent>(OnRevealResponse);
        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(OnGetInviteVerb);
        SubscribeLocalEvent<GetVerbsEvent<InteractionVerb>>(OnGetRevealVerb);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!TryComp<CompanyComponent>(args.Mob, out var comp)) return;
        var companyId = comp.CompanyName;
        if (string.IsNullOrWhiteSpace(companyId) || companyId == "None") return;
        BroadcastInvalidate(companyId);
    }

    private void OnMembersRequest(CompanyMembersRequestEvent ev, EntitySessionEventArgs args)
    {
        var requester = args.SenderSession.AttachedEntity;
        if (requester is not { } requesterEnt || !Exists(requesterEnt))
            return;

        var viewerCompanyId = TryComp<CompanyComponent>(requesterEnt, out var viewerCompany) && !string.IsNullOrWhiteSpace(viewerCompany.CompanyName)
            ? viewerCompany.CompanyName
            : "None";
        var canViewMembers = string.Equals(viewerCompanyId, ev.CompanyId, StringComparison.OrdinalIgnoreCase);

        var members = new List<CompanyMemberEntry>();

        if (canViewMembers)
        {
            var query = AllEntityQuery<CompanyComponent, MetaDataComponent, HumanoidAppearanceComponent>();
            while (query.MoveNext(out var uid, out var company, out var meta, out _))
            {
                if (!string.Equals(company.CompanyName, ev.CompanyId, StringComparison.OrdinalIgnoreCase))
                    continue;

                members.Add(new CompanyMemberEntry(GetNetEntity(uid), meta.EntityName));
            }

            members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        var viewerOwnsCompany = string.Equals(viewerCompanyId, ev.CompanyId, StringComparison.OrdinalIgnoreCase);
        var viewerIsLeader = viewerOwnsCompany && _prototypes.TryIndex<CompanyPrototype>(ev.CompanyId, out var proto) && IsLeader(args.SenderSession, proto);
        var warState = _factionWar.BuildUiState(args.SenderSession, ev.CompanyId);
        var motd = viewerOwnsCompany ? _motds.GetMotd(ev.CompanyId) : string.Empty;
        var canEditMotd = viewerOwnsCompany && _motds.CanSetMotd(args.SenderSession, ev.CompanyId);
        RaiseNetworkEvent(new CompanyMembersResponseEvent(ev.CompanyId, members, viewerIsLeader, viewerCompanyId, motd, canEditMotd, warState), Filter.SinglePlayer(args.SenderSession));
    }

    private void OnSetCompanyRequest(CompanySetCompanyRequestEvent ev, EntitySessionEventArgs args)
    {
        var desired = NormalizeRequestedCompany(ev.CompanyId);
        if (!_prototypes.HasIndex<CompanyPrototype>(desired)) return;
        if (IsCompanyInviteOnly(desired))
            return;

        if (args.SenderSession.AttachedEntity is not { } user || !Exists(user) || HasComp<GhostComponent>(user))
        {
            SyncLobbyCompany(args.SenderSession, desired);
            return;
        }

        var current = TryComp<CompanyComponent>(user, out var comp) && !string.IsNullOrWhiteSpace(comp.CompanyName)
            ? comp.CompanyName
            : "None";

        if (string.Equals(current, desired, StringComparison.OrdinalIgnoreCase))
        {
            SyncLobbyCompany(args.SenderSession, desired);
            return;
        }

        SetCompany(user, current, desired);
    }

    private void OnKickRequest(CompanyKickRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !Exists(user)) return;
        if (!_prototypes.TryIndex<CompanyPrototype>(ev.CompanyId, out var proto)) return;
        if (!IsCompanyInviteOnly(ev.CompanyId)) return;
        if (!TryComp<CompanyComponent>(user, out var userCompany) || !string.Equals(userCompany.CompanyName, ev.CompanyId, StringComparison.OrdinalIgnoreCase) || !IsLeader(args.SenderSession, proto))
        {
            _popup.PopupEntity(Loc.GetString("company-kick-failed-not-leader"), user, user);
            return;
        }
        if (!TryGetEntity(ev.Target, out var targetUidNullable) || targetUidNullable is not { } target || !Exists(target)) return;
        if (target == user)
        {
            _popup.PopupEntity(Loc.GetString("company-kick-failed-self"), user, user);
            return;
        }
        if (!TryComp<CompanyComponent>(target, out var targetCompany) || !string.Equals(targetCompany.CompanyName, ev.CompanyId, StringComparison.OrdinalIgnoreCase)) return;
        SetCompany(target, ev.CompanyId, "Neutral");
    }

    private void OnDeclareWarRequest(CompanyDeclareWarRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !Exists(user))
            return;

        var result = _factionWar.TryDeclareWar(args.SenderSession, ev.TargetCompanyId, ev.AnnouncementText);
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Error))
            _popup.PopupEntity(result.Error, user, user);
    }

    private void OnEndWarRequest(CompanyEndWarRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !Exists(user))
            return;

        var result = _factionWar.TryEndWar(args.SenderSession, ev.WarId);
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Error))
            _popup.PopupEntity(result.Error, user, user);
        else if (!string.IsNullOrWhiteSpace(result.Message))
            _popup.PopupEntity(result.Message, user, user);
    }

    private void OnGetInviteVerb(GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess) return;
        var targetUid = args.Target;
        if (args.User == targetUid) return;
        if (!HasComp<ActorComponent>(args.User)) return;
        if (!HasComp<HumanoidAppearanceComponent>(targetUid)) return;
        if (!TryComp<CompanyComponent>(args.User, out var userCompany) || string.IsNullOrWhiteSpace(userCompany.CompanyName) || userCompany.CompanyName == "None") return;
        if (IsCompanyInviteOnly(userCompany.CompanyName) && (!TryComp<ActorComponent>(args.User, out var actor) || !_prototypes.TryIndex<CompanyPrototype>(userCompany.CompanyName, out var proto) || !IsLeader(actor.PlayerSession, proto))) { return; }
        if (TryComp<CompanyComponent>(targetUid, out var targetCompany) && string.Equals(targetCompany.CompanyName, userCompany.CompanyName, StringComparison.OrdinalIgnoreCase)) return;
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("company-verb-invite"), Priority = 1, Act = () => SendInvite(args.User, targetUid, userCompany.CompanyName) });
    }

    private void OnGetRevealVerb(GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.User == args.Target)
            return;

        var targetUid = args.Target;

        if (!HasComp<ActorComponent>(args.User) || !HasComp<HumanoidAppearanceComponent>(targetUid))
            return;

        if (!TryComp<CompanyComponent>(targetUid, out var targetCompany) || !_companySystem.NeedsFactionRevealRequest(targetUid, args.User, targetCompany))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("company-verb-request-reveal"),
            Priority = 2,
            Act = () => SendRevealRequest(args.User, targetUid)
        });
    }

    private void SendInvite(EntityUid inviter, EntityUid target, string companyId)
    {
        if (!Exists(inviter) || !Exists(target)) return;
        if (!TryComp<CompanyComponent>(inviter, out var inviterCompany) || !string.Equals(inviterCompany.CompanyName, companyId, StringComparison.OrdinalIgnoreCase)) return;
        if (!TryComp<ActorComponent>(inviter, out var inviterActor)) return;
        if (!TryComp<ActorComponent>(target, out var targetActor)) return;
        if (IsCompanyInviteOnly(companyId))
        {
            if (!_prototypes.TryIndex<CompanyPrototype>(companyId, out var proto) || !IsLeader(inviterActor.PlayerSession, proto))
            {
                _popup.PopupEntity(Loc.GetString("company-invite-failed-not-leader"), inviter, inviter);
                return;
            }
        }
        var inviteId = _nextInviteId++;
        var inviterName = MetaData(inviter).EntityName;
        var display = GetCompanyDisplayName(companyId);
        _pendingInvites[inviteId] = new PendingInvite(inviteId, inviterActor.PlayerSession, targetActor.PlayerSession, inviter, target, companyId);
        RaiseNetworkEvent(new CompanyInviteEvent(inviteId, inviterName, companyId, display), Filter.SinglePlayer(targetActor.PlayerSession));
    }

    private void OnInviteResponse(CompanyInviteResponseEvent ev, EntitySessionEventArgs args)
    {
        if (!_pendingInvites.TryGetValue(ev.InviteId, out var invite)) return;
        if (invite.Target != args.SenderSession) return;
        _pendingInvites.Remove(ev.InviteId);
        var targetEnt = invite.TargetEntity;
        var inviterEnt = invite.InviterEntity;
        if (!Exists(targetEnt) || !Exists(inviterEnt)) return;
        if (!ev.Accept) return;
        var desired = invite.CompanyId;
        var current = TryComp<CompanyComponent>(targetEnt, out var targetCompany) && !string.IsNullOrWhiteSpace(targetCompany.CompanyName) ? targetCompany.CompanyName : "None";
        if (IsCompanyInviteOnly(current) && !string.Equals(current, desired, StringComparison.OrdinalIgnoreCase))
        {
            _popup.PopupEntity(Loc.GetString("company-invite-failed-private"), targetEnt, targetEnt);
            return;
        }
        if (IsCompanyInviteOnly(desired))
        {
            if (!_prototypes.TryIndex<CompanyPrototype>(desired, out var proto) || !IsLeader(invite.Inviter, proto))
            {
                _popup.PopupEntity(Loc.GetString("company-invite-failed-not-leader"), targetEnt, targetEnt);
                return;
            }
        }
        SetCompany(targetEnt, current, desired);
    }

    private void SendRevealRequest(EntityUid requester, EntityUid target)
    {
        if (!Exists(requester) || !Exists(target))
            return;

        if (!TryComp<ActorComponent>(requester, out var requesterActor) || !TryComp<ActorComponent>(target, out var targetActor))
            return;

        if (!_companySystem.NeedsFactionRevealRequest(target, requester))
            return;

        var requestId = _nextRevealRequestId++;
        var requesterName = MetaData(requester).EntityName;
        _pendingRevealRequests[requestId] = new PendingRevealRequest(requestId, requesterActor.PlayerSession, targetActor.PlayerSession, requester, target);
        RaiseNetworkEvent(new CompanyRevealRequestEvent(requestId, requesterName), Filter.SinglePlayer(targetActor.PlayerSession));
        _popup.PopupEntity(Loc.GetString("company-reveal-request-sent", ("target", MetaData(target).EntityName)), requester, requester);
    }

    private void OnRevealResponse(CompanyRevealResponseEvent ev, EntitySessionEventArgs args)
    {
        if (!_pendingRevealRequests.TryGetValue(ev.RequestId, out var request))
            return;

        if (request.Target != args.SenderSession)
            return;

        _pendingRevealRequests.Remove(ev.RequestId);

        if (!Exists(request.RequesterEntity) || !Exists(request.TargetEntity))
            return;

        if (!ev.Accept)
        {
            _popup.PopupEntity(Loc.GetString("company-reveal-request-denied", ("target", MetaData(request.TargetEntity).EntityName)), request.RequesterEntity, request.RequesterEntity);
            return;
        }

        _companySystem.RevealCompanyTo(request.TargetEntity, request.Requester);
        _popup.PopupEntity(Loc.GetString("company-reveal-request-approved-requester", ("target", MetaData(request.TargetEntity).EntityName)), request.RequesterEntity, request.RequesterEntity);
        _popup.PopupEntity(Loc.GetString("company-reveal-request-approved-target", ("requester", MetaData(request.RequesterEntity).EntityName)), request.TargetEntity, request.TargetEntity);
    }

    private void BroadcastInvalidate(string companyId)
    { RaiseNetworkEvent(new CompanyMembersInvalidateEvent(companyId), Filter.Empty().AddAllPlayers(_players)); }

    private void SetCompany(EntityUid target, string oldCompany, string newCompany)
    {
        _companySystem.SetCompany(target, newCompany);
        _companySystem.UpdateStoredCompanyPreference(target, newCompany);
        SyncLobbyCompany(target, newCompany);
        if (!string.IsNullOrWhiteSpace(oldCompany) && oldCompany != "None") BroadcastInvalidate(oldCompany);
        if (!string.IsNullOrWhiteSpace(newCompany) && newCompany != "None") BroadcastInvalidate(newCompany);
    }

    private string NormalizeRequestedCompany(string? companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId) || string.Equals(companyId, "None", StringComparison.OrdinalIgnoreCase))
            return "Neutral";

        return companyId;
    }

    private async void SyncLobbyCompany(EntityUid target, string newCompany)
    {
        if (!TryComp<ActorComponent>(target, out var actor))
            return;

        if (!_prefsManager.TryGetCachedPreferences(actor.PlayerSession.UserId, out var prefs))
            return;

        var selectedIndex = prefs.SelectedCharacterIndex;
        if (!prefs.Characters.TryGetValue(selectedIndex, out var profile) || profile is not HumanoidCharacterProfile humanoid)
            return;

        if (string.Equals(humanoid.Company, newCompany, StringComparison.OrdinalIgnoreCase))
            return;

        await _prefsManager.SetProfile(actor.PlayerSession.UserId, selectedIndex, humanoid.WithCompany(newCompany), validateFields: false);
        await _prefsManager.RefreshPreferencesAsync(actor.PlayerSession, CancellationToken.None);
    }

    private async void SyncLobbyCompany(ICommonSession session, string newCompany)
    {
        if (!_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
            return;

        var selectedIndex = prefs.SelectedCharacterIndex;
        if (!prefs.Characters.TryGetValue(selectedIndex, out var profile) || profile is not HumanoidCharacterProfile humanoid)
            return;

        if (string.Equals(humanoid.Company, newCompany, StringComparison.OrdinalIgnoreCase))
            return;

        await _prefsManager.SetProfile(session.UserId, selectedIndex, humanoid.WithCompany(newCompany), validateFields: false);
        await _prefsManager.RefreshPreferencesAsync(session, CancellationToken.None);
    }

    private bool IsCompanyInviteOnly(string companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId) || companyId is "None" or "Neutral")
            return false;

        return _prototypes.TryIndex<CompanyPrototype>(companyId, out var proto) && (proto.Disabled || proto.HiddenFromNonMembers);
    }

    private bool IsLeader(ICommonSession session, CompanyPrototype proto)
    {
        if (proto.LeaderJobs.Count == 0) return false;
        var mind = _playerSystem.ContentData(session)?.Mind;
        if (mind == null) return false;
        foreach (var jobId in proto.LeaderJobs)
        { if (_jobs.MindHasJobWithId(mind, jobId)) return true; }
        return false;
    }

    private sealed record PendingInvite(int Id, ICommonSession Inviter, ICommonSession Target, EntityUid InviterEntity, EntityUid TargetEntity, string CompanyId);
    private sealed record PendingRevealRequest(int Id, ICommonSession Requester, ICommonSession Target, EntityUid RequesterEntity, EntityUid TargetEntity);

    private bool IsCompanyHiddenFromNonMembers(string companyId)
    { return _prototypes.TryIndex<CompanyPrototype>(companyId, out var proto) && proto.HiddenFromNonMembers; }

    private string GetCompanyDisplayName(string companyId)
    { return _prototypes.TryIndex<CompanyPrototype>(companyId, out var proto) ? proto.Name : companyId; }
}

