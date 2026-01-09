// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Company;
using Content.Shared._Mono.Company;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Roles.Jobs;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Company;

public sealed class CompanyInviteSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedPlayerSystem _playerSystem = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    private int _nextInviteId = 1;
    private readonly Dictionary<int, PendingInvite> _pendingInvites = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CompanyMembersRequestEvent>(OnMembersRequest);
        SubscribeNetworkEvent<CompanySetCompanyRequestEvent>(OnSetCompanyRequest);
        SubscribeNetworkEvent<CompanyKickRequestEvent>(OnKickRequest);
        SubscribeNetworkEvent<CompanyInviteResponseEvent>(OnInviteResponse);
        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(OnGetInviteVerb);
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
        if (requester is not { } requesterEnt || !Exists(requesterEnt)) return;
        if (IsCompanyHiddenFromNonMembers(ev.CompanyId))
        { if (!TryComp<CompanyComponent>(requesterEnt, out var requesterCompany) || !string.Equals(requesterCompany.CompanyName, ev.CompanyId, StringComparison.OrdinalIgnoreCase)) return; }
        var members = new List<CompanyMemberEntry>();
        var query = AllEntityQuery<CompanyComponent, MetaDataComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out var company, out var meta, out _))
        {
            if (!string.Equals(company.CompanyName, ev.CompanyId, StringComparison.OrdinalIgnoreCase)) continue;
            members.Add(new CompanyMemberEntry(GetNetEntity(uid), meta.EntityName));
        }
        members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        var viewerCompanyId = TryComp<CompanyComponent>(requesterEnt, out var viewerCompany) && !string.IsNullOrWhiteSpace(viewerCompany.CompanyName) ? viewerCompany.CompanyName : "None";
        var viewerIsLeader = viewerCompanyId == ev.CompanyId && _prototypes.TryIndex<CompanyPrototype>(ev.CompanyId, out var proto) && IsLeader(args.SenderSession, proto);
        RaiseNetworkEvent(new CompanyMembersResponseEvent(ev.CompanyId, members, viewerIsLeader, viewerCompanyId), Filter.SinglePlayer(args.SenderSession));
    }

    private void OnSetCompanyRequest(CompanySetCompanyRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !Exists(user)) return;
        if (HasComp<GhostComponent>(user)) return;
        var desired = string.IsNullOrWhiteSpace(ev.CompanyId) ? "None" : ev.CompanyId;
        if (desired != "None" && !_prototypes.HasIndex<CompanyPrototype>(desired)) return;
        var current = TryComp<CompanyComponent>(user, out var comp) && !string.IsNullOrWhiteSpace(comp.CompanyName) ? comp.CompanyName : "None";
        if (string.Equals(current, desired, StringComparison.OrdinalIgnoreCase)) return;
        if (IsCompanyPrivate(current))
        {
            _popup.PopupEntity(Loc.GetString("company-invite-failed-private"), user, user);
            return;
        }
        if (IsCompanyPrivate(desired)) return;
        SetCompany(user, current, desired);
    }

    private void OnKickRequest(CompanyKickRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !Exists(user)) return;
        if (!_prototypes.TryIndex<CompanyPrototype>(ev.CompanyId, out var proto)) return;
        if (!IsCompanyPrivate(ev.CompanyId)) return;
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
        SetCompany(target, ev.CompanyId, "None");
    }

    private void OnGetInviteVerb(GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess) return;
        var targetUid = args.Target;
        if (args.User == targetUid) return;
        if (!HasComp<ActorComponent>(args.User)) return;
        if (!HasComp<HumanoidAppearanceComponent>(targetUid)) return;
        if (!TryComp<CompanyComponent>(args.User, out var userCompany) || string.IsNullOrWhiteSpace(userCompany.CompanyName) || userCompany.CompanyName == "None") return;
        if (IsCompanyPrivate(userCompany.CompanyName) && (!TryComp<ActorComponent>(args.User, out var actor) || !_prototypes.TryIndex<CompanyPrototype>(userCompany.CompanyName, out var proto) || !IsLeader(actor.PlayerSession, proto))) { return; }
        if (TryComp<CompanyComponent>(targetUid, out var targetCompany) && string.Equals(targetCompany.CompanyName, userCompany.CompanyName, StringComparison.OrdinalIgnoreCase)) return;
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("company-verb-invite"), Priority = 1, Act = () => SendInvite(args.User, targetUid, userCompany.CompanyName) });
    }

    private void SendInvite(EntityUid inviter, EntityUid target, string companyId)
    {
        if (!Exists(inviter) || !Exists(target)) return;
        if (!TryComp<CompanyComponent>(inviter, out var inviterCompany) || !string.Equals(inviterCompany.CompanyName, companyId, StringComparison.OrdinalIgnoreCase)) return;
        if (!TryComp<ActorComponent>(inviter, out var inviterActor)) return;
        if (!TryComp<ActorComponent>(target, out var targetActor)) return;
        if (IsCompanyPrivate(companyId))
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
        if (IsCompanyPrivate(current) && !string.Equals(current, desired, StringComparison.OrdinalIgnoreCase))
        {
            _popup.PopupEntity(Loc.GetString("company-invite-failed-private"), targetEnt, targetEnt);
            return;
        }
        if (IsCompanyPrivate(desired))
        {
            if (!_prototypes.TryIndex<CompanyPrototype>(desired, out var proto) || !IsLeader(invite.Inviter, proto))
            {
                _popup.PopupEntity(Loc.GetString("company-invite-failed-not-leader"), targetEnt, targetEnt);
                return;
            }
        }
        SetCompany(targetEnt, current, desired);
    }

    private void BroadcastInvalidate(string companyId)
    { RaiseNetworkEvent(new CompanyMembersInvalidateEvent(companyId), Filter.Empty().AddAllPlayers(_players)); }

    private void SetCompany(EntityUid target, string oldCompany, string newCompany)
    {
        var comp = EnsureComp<CompanyComponent>(target);
        comp.CompanyName = newCompany;
        Dirty(target, comp);
        if (!string.IsNullOrWhiteSpace(oldCompany) && oldCompany != "None") BroadcastInvalidate(oldCompany);
        if (!string.IsNullOrWhiteSpace(newCompany) && newCompany != "None") BroadcastInvalidate(newCompany);
    }

    private bool IsCompanyPrivate(string companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId) || companyId == "None") return false;
        return _prototypes.TryIndex<CompanyPrototype>(companyId, out var proto) && proto.Disabled;
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

    private bool IsCompanyHiddenFromNonMembers(string companyId)
    { return _prototypes.TryIndex<CompanyPrototype>(companyId, out var proto) && proto.HiddenFromNonMembers; }

    private string GetCompanyDisplayName(string companyId)
    { return _prototypes.TryIndex<CompanyPrototype>(companyId, out var proto) ? proto.Name : companyId; }
}

