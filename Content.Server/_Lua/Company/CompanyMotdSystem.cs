// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Administration.Logs;
using Content.Shared._Lua.Company;
using Content.Shared._Mono.Company;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Company;

public sealed class CompanyMotdSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedPlayerSystem _player = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly Dictionary<string, string> _roundMotds = new(StringComparer.OrdinalIgnoreCase);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CompanySetMotdRequestEvent>(OnSetMotdRequest);
    }

    public string GetMotd(string companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return string.Empty;

        return _roundMotds.TryGetValue(companyId, out var motd)
            ? motd
            : string.Empty;
    }

    public bool CanSetMotd(ICommonSession session, string companyId)
    {
        return IsLeader(session, companyId);
    }

    private void OnSetMotdRequest(CompanySetMotdRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !Exists(user))
            return;

        if (string.IsNullOrWhiteSpace(ev.CompanyId) || !_prototypes.HasIndex<CompanyPrototype>(ev.CompanyId))
            return;

        if (!CanSetMotd(args.SenderSession, ev.CompanyId))
        {
            _popup.PopupEntity(Loc.GetString("company-motd-error-leader-only"), user, user);
            return;
        }

        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var sanitized = SharedChatSystem.SanitizeAnnouncement(ev.Motd, maxLength).Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
            _roundMotds.Remove(ev.CompanyId);
        else
            _roundMotds[ev.CompanyId] = sanitized;

        _adminLogger.Add(LogType.Chat, LogImpact.Low,
            $"{args.SenderSession.Name} set company MOTD for {ev.CompanyId}: {sanitized}");

        RaiseNetworkEvent(new CompanyMembersInvalidateEvent(ev.CompanyId), Filter.Empty().AddAllPlayers(_players));
        _popup.PopupEntity(Loc.GetString("company-motd-updated"), user, user);
    }

    private bool IsLeader(ICommonSession session, string companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId) || !_prototypes.TryIndex<CompanyPrototype>(companyId, out var proto) || proto.LeaderJobs.Count == 0)
            return false;

        var mind = _player.ContentData(session)?.Mind;
        if (mind == null)
            return false;

        foreach (var jobId in proto.LeaderJobs)
        {
            if (_jobs.MindHasJobWithId(mind, jobId))
                return true;
        }

        return false;
    }
}
