// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server._NF.RoundNotifications.Events;
using Content.Server._Lua.Company.Components;
using Content.Server._Mono.Company;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared._Lua.Announce;
using Content.Shared._Lua.Company;
using Content.Shared.Lua.CLVar;
using Content.Shared._Mono;
using Content.Shared._Mono.Company;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Players;
using Content.Shared.Roles.Jobs;
using Content.Shared.Tiles;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Company;

public sealed class FactionWarSystem : EntitySystem
{
    private static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);
    private const string MoscowTimeZoneLabel = "МСК";

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly ProtectedGridSystem _protectedGrid = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedPlayerSystem _player = default!;

    private readonly Dictionary<int, ActiveFactionWar> _activeWars = new();
    private readonly Dictionary<string, HashSet<int>> _companyToWars = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EntityUid, ProtectedGridState> _suppressedProtectedGrids = new();
    private readonly HashSet<EntityUid> _suppressedGridGodMode = new();
    private bool _warPrimeProtectionSuppressed;
    private int _nextWarId = 1;
    private DateTimeOffset? _roundStartedAt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGridAdded);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundStarted(RoundStartedEvent _)
    {
        _roundStartedAt = DateTimeOffset.Now;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        _roundStartedAt = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = DateTimeOffset.Now;
        UpdateWarPrimeStationProtection(now);

        if (_activeWars.Count == 0)
            return;

        List<int>? finishedWars = null;

        foreach (var (warId, war) in _activeWars)
        {
            if (war.EndTime > now)
                continue;

            finishedWars ??= new List<int>();
            finishedWars.Add(warId);
        }

        if (finishedWars == null)
            return;

        foreach (var warId in finishedWars)
        {
            if (_activeWars.TryGetValue(warId, out var war))
            {
                EndWarInternal(
                    war,
                    Loc.GetString("company-war-ended-global-timeout"),
                    Loc.GetString("company-war-briefing-ended-timeout"),
                    $"Faction war #{war.Id} timed out: {GetCompanyDisplayName(war.AggressorCompanyId)} vs {GetCompanyDisplayName(war.DefenderCompanyId)}");
            }
        }
    }

    private void OnStationGridAdded(StationGridAddedEvent args)
    {
        if (!HasComp<FactionOwnedStationComponent>(args.Station))
            return;

        if (_warPrimeProtectionSuppressed)
            SuppressGridProtection(args.GridId);
        else
            RestoreGridProtection(args.GridId);
    }

    public CompanyWarUiState BuildUiState(ICommonSession session, string selectedCompanyId)
    {
        var viewerCompanyId = GetViewerCompanyId(session);
        var viewerIsLeader = IsLeader(session, viewerCompanyId);
        var now = DateTimeOffset.Now;
        var activeWars = GetActiveWarOverviews(viewerCompanyId, now);

        TryGetWarPair(viewerCompanyId, selectedCompanyId, out var selectedWar);

        var canEndWar = selectedWar != null && viewerIsLeader;
        var canDeclareWar = false;
        string statusText;

        if (!_cfg.GetCVar(CLVars.FactionWarEnabled))
        {
            statusText = Loc.GetString("company-war-status-disabled");
        }
        else if (string.Equals(viewerCompanyId, "None", StringComparison.OrdinalIgnoreCase))
        {
            statusText = Loc.GetString("company-war-status-no-company");
        }
        else if (!viewerIsLeader)
        {
            statusText = Loc.GetString("company-war-status-leader-only");
        }
        else if (string.IsNullOrWhiteSpace(selectedCompanyId) || string.Equals(selectedCompanyId, viewerCompanyId, StringComparison.OrdinalIgnoreCase))
        {
            statusText = Loc.GetString("company-war-status-select-target");
        }
        else if (selectedWar != null)
        {
            statusText = BuildPeaceStatusText(selectedWar, viewerCompanyId);
        }
        else if (!_prototypes.HasIndex<CompanyPrototype>(selectedCompanyId))
        {
            statusText = Loc.GetString("company-war-status-invalid-target");
        }
        else if (_prototypes.TryIndex<CompanyPrototype>(selectedCompanyId, out var targetProto)
                 && targetProto.Disabled
                 && !targetProto.HiddenFromNonMembers)
        {
            statusText = Loc.GetString("company-war-status-invalid-target");
        }
        else if (!IsWarDeclarationUnlocked(now, out var unlockTime, out var remaining))
        {
            var lockDays = _cfg.GetCVar(CLVars.FactionWarDeclarationLockDays);
            statusText = Loc.GetString(
                "company-war-status-round-lock",
                ("days", lockDays.ToString()),
                ("unlockTime", unlockTime.ToLocalTime().ToString("dd.MM HH:mm")),
                ("remaining", FormatRemaining(remaining)));
        }
        else if (!IsPrimeTime(now))
        {
            statusText = GetPrimeTimeText("company-war-status-prime-time", now);
        }
        else
        {
            canDeclareWar = true;
            statusText = Loc.GetString("company-war-status-ready", ("company", GetCompanyDisplayName(selectedCompanyId)));
        }

        return new CompanyWarUiState(
            viewerCompanyId,
            viewerIsLeader,
            canDeclareWar,
            canEndWar,
            statusText,
                selectedWar == null ? null : BuildOverview(selectedWar, now),
                activeWars);
    }

            public List<CompanyWarOverview> GetActiveWarOverviews(string companyId)
            {
            return GetActiveWarOverviews(companyId, DateTimeOffset.Now);
            }

    public CompanyWarActionResult TryDeclareWar(ICommonSession session, string targetCompanyId, string announcementText)
    {
        var validationError = ValidateWarDeclaration(session, targetCompanyId, DateTimeOffset.Now);
        if (validationError != null)
            return new CompanyWarActionResult(false, validationError);

        var viewerCompanyId = GetViewerCompanyId(session);
        var now = DateTimeOffset.Now;
        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var sanitized = SharedChatSystem.SanitizeAnnouncement(announcementText, maxLength).Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = Loc.GetString("company-war-default-announcement", ("aggressor", GetCompanyDisplayName(viewerCompanyId)), ("defender", GetCompanyDisplayName(targetCompanyId)));
        }

        var war = new ActiveFactionWar(
            _nextWarId++,
            viewerCompanyId,
            targetCompanyId,
            GetDeclarerName(session),
            sanitized,
            now,
            GetWarEndTime(now));

        _activeWars[war.Id] = war;
        AddWarIndex(war.AggressorCompanyId, war.Id);
        AddWarIndex(war.DefenderCompanyId, war.Id);

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{session.Name} declared faction war #{war.Id}: {GetCompanyDisplayName(war.AggressorCompanyId)} vs {GetCompanyDisplayName(war.DefenderCompanyId)}. Announcement: {war.AnnouncementText}");

        AnnounceWarStart(war, session.AttachedEntity);
        SendWarBriefing(war, Loc.GetString(
            "company-war-briefing-start",
            ("aggressor", GetCompanyDisplayName(war.AggressorCompanyId)),
            ("defender", GetCompanyDisplayName(war.DefenderCompanyId)),
            ("declaredBy", war.DeclaredBy),
            ("endTime", war.EndTime.ToLocalTime().ToString("HH:mm:ss")),
            ("message", war.AnnouncementText)));
        InvalidateCompany(war.AggressorCompanyId);
        InvalidateCompany(war.DefenderCompanyId);

        return new CompanyWarActionResult(true, null, null, war.Id);
    }

    public bool ForceDeclareWar(string aggressorCompanyId, string defenderCompanyId, string declaredBy, string announcementText, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(aggressorCompanyId) || !_prototypes.HasIndex<CompanyPrototype>(aggressorCompanyId))
        {
            error = Loc.GetString("company-war-error-invalid-target");
            return false;
        }

        if (string.IsNullOrWhiteSpace(defenderCompanyId) || !_prototypes.HasIndex<CompanyPrototype>(defenderCompanyId))
        {
            error = Loc.GetString("company-war-error-invalid-target");
            return false;
        }

        if (string.Equals(aggressorCompanyId, defenderCompanyId, StringComparison.OrdinalIgnoreCase))
        {
            error = Loc.GetString("company-war-error-self-target");
            return false;
        }

        if (_prototypes.TryIndex<CompanyPrototype>(defenderCompanyId, out var targetProto)
            && targetProto.Disabled
            && !targetProto.HiddenFromNonMembers)
        {
            error = Loc.GetString("company-war-error-invalid-target");
            return false;
        }

        if (TryGetWarPair(aggressorCompanyId, defenderCompanyId, out _))
        {
            error = Loc.GetString("company-war-error-already-at-war", ("company", GetCompanyDisplayName(defenderCompanyId)));
            return false;
        }

        var now = DateTimeOffset.Now;
        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var sanitized = SharedChatSystem.SanitizeAnnouncement(announcementText, maxLength).Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = Loc.GetString("company-war-default-announcement", ("aggressor", GetCompanyDisplayName(aggressorCompanyId)), ("defender", GetCompanyDisplayName(defenderCompanyId)));
        }

        var war = new ActiveFactionWar(
            _nextWarId++,
            aggressorCompanyId,
            defenderCompanyId,
            declaredBy,
            sanitized,
            now,
            GetWarEndTime(now));

        _activeWars[war.Id] = war;
        AddWarIndex(war.AggressorCompanyId, war.Id);
        AddWarIndex(war.DefenderCompanyId, war.Id);

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{declaredBy} force-declared faction war #{war.Id}: {GetCompanyDisplayName(war.AggressorCompanyId)} vs {GetCompanyDisplayName(war.DefenderCompanyId)}. Announcement: {war.AnnouncementText}");

        AnnounceWarStart(war, null);
        SendWarBriefing(war, Loc.GetString(
            "company-war-briefing-start",
            ("aggressor", GetCompanyDisplayName(war.AggressorCompanyId)),
            ("defender", GetCompanyDisplayName(war.DefenderCompanyId)),
            ("declaredBy", war.DeclaredBy),
            ("endTime", war.EndTime.ToLocalTime().ToString("HH:mm:ss")),
            ("message", war.AnnouncementText)));
        InvalidateCompany(war.AggressorCompanyId);
        InvalidateCompany(war.DefenderCompanyId);
        return true;
    }

    public CompanyWarActionResult TryEndWar(ICommonSession session, int warId)
    {
        if (!_activeWars.TryGetValue(warId, out var war))
            return new CompanyWarActionResult(false, Loc.GetString("company-war-error-not-found"));

        var viewerCompanyId = GetViewerCompanyId(session);
        if (!string.Equals(viewerCompanyId, war.AggressorCompanyId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(viewerCompanyId, war.DefenderCompanyId, StringComparison.OrdinalIgnoreCase))
        {
            return new CompanyWarActionResult(false, Loc.GetString("company-war-error-not-participant"));
        }

        if (!IsLeader(session, viewerCompanyId))
            return new CompanyWarActionResult(false, Loc.GetString("company-war-error-leader-only"));

        var otherCompanyId = string.Equals(viewerCompanyId, war.AggressorCompanyId, StringComparison.OrdinalIgnoreCase)
            ? war.DefenderCompanyId
            : war.AggressorCompanyId;

        if (!war.PeaceRequests.Add(viewerCompanyId))
            return new CompanyWarActionResult(true, null, Loc.GetString("company-war-end-request-already-sent"), war.Id);

        if (!war.PeaceRequests.Contains(otherCompanyId))
        {
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"{session.Name} requested mutual end for faction war #{war.Id}: {GetCompanyDisplayName(war.AggressorCompanyId)} vs {GetCompanyDisplayName(war.DefenderCompanyId)}");

            SendWarBriefing(war, Loc.GetString("company-war-briefing-end-request", ("company", GetCompanyDisplayName(viewerCompanyId))));
            InvalidateCompany(war.AggressorCompanyId);
            InvalidateCompany(war.DefenderCompanyId);
            return new CompanyWarActionResult(true, null, Loc.GetString("company-war-end-request-sent"), war.Id);
        }

        EndWarInternal(
            war,
            Loc.GetString("company-war-ended-global-mutual", ("aggressor", GetCompanyDisplayName(war.AggressorCompanyId)), ("defender", GetCompanyDisplayName(war.DefenderCompanyId))),
            Loc.GetString("company-war-briefing-ended-mutual", ("aggressor", GetCompanyDisplayName(war.AggressorCompanyId)), ("defender", GetCompanyDisplayName(war.DefenderCompanyId))),
            $"{session.Name} mutually ended faction war #{war.Id}: {GetCompanyDisplayName(war.AggressorCompanyId)} vs {GetCompanyDisplayName(war.DefenderCompanyId)}");
        return new CompanyWarActionResult(true, null, null, war.Id);
    }

    public bool TryGetActiveWarId(string companyId, out int warId)
    {
        warId = 0;

        if (!TryGetFirstActiveWar(companyId, out var war) || war == null)
            return false;

        warId = war.Id;
        return true;
    }

    public bool HasAnyActiveWars(string companyId)
    {
        return !string.IsNullOrWhiteSpace(companyId)
               && _companyToWars.TryGetValue(companyId, out var warIds)
               && warIds.Count > 0;
    }

    public bool TryGetWarBetween(string firstCompanyId, string secondCompanyId, out int warId)
    {
        warId = 0;

        if (!TryGetWarPair(firstCompanyId, secondCompanyId, out var war) || war == null)
            return false;

        warId = war.Id;
        return true;
    }

    public List<CompanyWarOverview> GetAllActiveWarOverviews()
    {
        var now = DateTimeOffset.Now;
        var overviews = new List<CompanyWarOverview>(_activeWars.Count);

        foreach (var war in _activeWars.Values)
        {
            overviews.Add(BuildOverview(war, now));
        }

        overviews.Sort((a, b) => a.RemainingSeconds.CompareTo(b.RemainingSeconds));
        return overviews;
    }

    public bool ForceEndWar(int warId, string endedBy, out string? error)
    {
        error = null;

        if (!_activeWars.TryGetValue(warId, out var war))
        {
            error = Loc.GetString("company-war-error-not-found");
            return false;
        }

        EndWarInternal(
            war,
            Loc.GetString("company-war-ended-global-kvo", ("aggressor", GetCompanyDisplayName(war.AggressorCompanyId)), ("defender", GetCompanyDisplayName(war.DefenderCompanyId))),
            Loc.GetString("company-war-briefing-ended-kvo", ("aggressor", GetCompanyDisplayName(war.AggressorCompanyId)), ("defender", GetCompanyDisplayName(war.DefenderCompanyId))),
            $"{endedBy} force-ended faction war #{war.Id}: {GetCompanyDisplayName(war.AggressorCompanyId)} vs {GetCompanyDisplayName(war.DefenderCompanyId)}");
        return true;
    }

    private string? ValidateWarDeclaration(ICommonSession session, string targetCompanyId, DateTimeOffset now)
    {
        if (!_cfg.GetCVar(CLVars.FactionWarEnabled))
            return Loc.GetString("company-war-error-disabled");

        var viewerCompanyId = GetViewerCompanyId(session);

        if (string.Equals(viewerCompanyId, "None", StringComparison.OrdinalIgnoreCase))
            return Loc.GetString("company-war-error-no-company");

        if (string.IsNullOrWhiteSpace(targetCompanyId) || !_prototypes.HasIndex<CompanyPrototype>(targetCompanyId))
            return Loc.GetString("company-war-error-invalid-target");

        if (_prototypes.TryIndex<CompanyPrototype>(targetCompanyId, out var targetProto)
            && targetProto.Disabled
            && !targetProto.HiddenFromNonMembers)
        {
            return Loc.GetString("company-war-error-invalid-target");
        }

        if (string.Equals(viewerCompanyId, targetCompanyId, StringComparison.OrdinalIgnoreCase))
            return Loc.GetString("company-war-error-self-target");

        if (!IsLeader(session, viewerCompanyId))
            return Loc.GetString("company-war-error-leader-only");

        if (TryGetWarPair(viewerCompanyId, targetCompanyId, out _))
            return Loc.GetString("company-war-error-already-at-war", ("company", GetCompanyDisplayName(targetCompanyId)));

        if (!IsWarDeclarationUnlocked(now, out var unlockTime, out var remaining))
        {
            var lockDays = _cfg.GetCVar(CLVars.FactionWarDeclarationLockDays);
            return Loc.GetString(
                "company-war-error-round-lock",
                ("days", lockDays.ToString()),
                ("unlockTime", unlockTime.ToLocalTime().ToString("dd.MM HH:mm")),
                ("remaining", FormatRemaining(remaining)));
        }

        if (!IsPrimeTime(now))
            return GetPrimeTimeText("company-war-error-prime-time", now);

        return null;
    }

    private void EndWarInternal(ActiveFactionWar war, string globalMessage, string briefingMessage, string adminLogMessage)
    {
        _activeWars.Remove(war.Id);
        RemoveWarIndex(war.AggressorCompanyId, war.Id);
        RemoveWarIndex(war.DefenderCompanyId, war.Id);

        _adminLogger.Add(LogType.Action, LogImpact.High, $"{adminLogMessage}");

        _chat.DispatchGlobalAnnouncement(globalMessage, Loc.GetString("company-war-announcement-title"), true, new SoundPathSpecifier("/Audio/_Lua/Alarm/warmessage.ogg"), colorOverride: Color.OrangeRed);
        SendWarBriefing(war, briefingMessage);
        InvalidateCompany(war.AggressorCompanyId);
        InvalidateCompany(war.DefenderCompanyId);
    }

    private void AnnounceWarStart(ActiveFactionWar war, EntityUid? declarer)
    {
        var title = Loc.GetString(
            "company-war-overlay-title",
            ("aggressor", GetCompanyDisplayName(war.AggressorCompanyId)),
            ("defender", GetCompanyDisplayName(war.DefenderCompanyId)));

        _chat.DispatchGlobalAnnouncement(
            war.AnnouncementText,
            title,
            true,
            new SoundPathSpecifier("/Audio/_Lua/Alarm/warmessage.ogg"),
            colorOverride: Color.Red,
            speaker: declarer,
            announcementPreset: AnnouncementOverlayParams.PresetComms);
    }

    private void SendWarBriefing(ActiveFactionWar war, string briefing)
    {
        var sessions = new List<ICommonSession>();
        var query = AllEntityQuery<CompanyComponent, ActorComponent>();

        while (query.MoveNext(out _, out var company, out var actor))
        {
            if (!string.Equals(company.CompanyName, war.AggressorCompanyId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(company.CompanyName, war.DefenderCompanyId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sessions.Add(actor.PlayerSession);
        }

        _antag.SendBriefing(sessions, briefing, Color.OrangeRed, null);
    }

    private CompanyWarOverview BuildOverview(ActiveFactionWar war, DateTimeOffset now)
    {
        return new CompanyWarOverview(
            war.Id,
            war.AggressorCompanyId,
            GetCompanyDisplayName(war.AggressorCompanyId),
            war.DefenderCompanyId,
            GetCompanyDisplayName(war.DefenderCompanyId),
            war.DeclaredBy,
            war.AnnouncementText,
            MathF.Max(0f, (float) (war.EndTime - now).TotalSeconds),
            war.PeaceRequests.Contains(war.AggressorCompanyId),
            war.PeaceRequests.Contains(war.DefenderCompanyId));
    }

    private List<CompanyWarOverview> GetActiveWarOverviews(string companyId, DateTimeOffset now)
    {
        var overviews = new List<CompanyWarOverview>();

        if (string.IsNullOrWhiteSpace(companyId)
            || !_companyToWars.TryGetValue(companyId, out var warIds)
            || warIds.Count == 0)
        {
            return overviews;
        }

        foreach (var warId in warIds)
        {
            if (!_activeWars.TryGetValue(warId, out var war))
                continue;

            overviews.Add(BuildOverview(war, now));
        }

        overviews.Sort((a, b) => a.RemainingSeconds.CompareTo(b.RemainingSeconds));
        return overviews;
    }

    private bool TryGetFirstActiveWar(string companyId, out ActiveFactionWar? war)
    {
        war = null;

        if (string.IsNullOrWhiteSpace(companyId))
            return false;

        if (!_companyToWars.TryGetValue(companyId, out var warIds) || warIds.Count == 0)
            return false;

        foreach (var warId in warIds)
        {
            if (_activeWars.TryGetValue(warId, out war))
                return true;
        }

        return false;
    }

    private bool TryGetWarPair(string firstCompanyId, string secondCompanyId, out ActiveFactionWar? war)
    {
        war = null;

        if (string.IsNullOrWhiteSpace(firstCompanyId)
            || string.IsNullOrWhiteSpace(secondCompanyId)
            || !_companyToWars.TryGetValue(firstCompanyId, out var warIds)
            || warIds.Count == 0)
        {
            return false;
        }

        foreach (var warId in warIds)
        {
            if (!_activeWars.TryGetValue(warId, out var candidate))
                continue;

            if ((string.Equals(candidate.AggressorCompanyId, firstCompanyId, StringComparison.OrdinalIgnoreCase)
                 && string.Equals(candidate.DefenderCompanyId, secondCompanyId, StringComparison.OrdinalIgnoreCase))
                || (string.Equals(candidate.AggressorCompanyId, secondCompanyId, StringComparison.OrdinalIgnoreCase)
                 && string.Equals(candidate.DefenderCompanyId, firstCompanyId, StringComparison.OrdinalIgnoreCase)))
            {
                war = candidate;
                return true;
            }
        }

        return false;
    }

    private void AddWarIndex(string companyId, int warId)
    {
        if (!_companyToWars.TryGetValue(companyId, out var warIds))
        {
            warIds = new HashSet<int>();
            _companyToWars[companyId] = warIds;
        }

        warIds.Add(warId);
    }

    private void RemoveWarIndex(string companyId, int warId)
    {
        if (!_companyToWars.TryGetValue(companyId, out var warIds))
            return;

        warIds.Remove(warId);
        if (warIds.Count == 0)
            _companyToWars.Remove(companyId);
    }

    private string GetViewerCompanyId(ICommonSession session)
    {
        if (session.AttachedEntity is not { } entity || !TryComp<CompanyComponent>(entity, out var company))
            return "None";

        return string.IsNullOrWhiteSpace(company.CompanyName) ? "None" : company.CompanyName;
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

    private string BuildPeaceStatusText(ActiveFactionWar war, string viewerCompanyId)
    {
        var viewerRequested = war.PeaceRequests.Contains(viewerCompanyId);
        var otherCompanyId = string.Equals(viewerCompanyId, war.AggressorCompanyId, StringComparison.OrdinalIgnoreCase)
            ? war.DefenderCompanyId
            : war.AggressorCompanyId;
        var otherRequested = war.PeaceRequests.Contains(otherCompanyId);

        if (otherRequested && !viewerRequested)
            return Loc.GetString("company-war-status-peace-offer", ("company", GetCompanyDisplayName(otherCompanyId)));

        if (viewerRequested)
            return Loc.GetString("company-war-status-peace-pending", ("company", GetCompanyDisplayName(otherCompanyId)));

        return Loc.GetString("company-war-status-active", ("company", GetCompanyDisplayName(otherCompanyId)));
    }

    private bool IsPrimeTime(DateTimeOffset now)
    {
        var startHour = NormalizeHour(_cfg.GetCVar(CLVars.FactionWarPrimeStartHour));
        var endHour = NormalizeHour(_cfg.GetCVar(CLVars.FactionWarPrimeEndHour));
        var hour = now.Hour;

        if (startHour == endHour)
            return true;

        if (startHour < endHour)
            return hour >= startHour && hour < endHour;

        return hour >= startHour || hour < endHour;
    }

    public bool IsWarDeclarationUnlocked(DateTimeOffset now, out DateTimeOffset unlockTime, out TimeSpan remaining)
    {
        var lockDays = Math.Max(0, _cfg.GetCVar(CLVars.FactionWarDeclarationLockDays));

        if (lockDays == 0)
        {
            unlockTime = now;
            remaining = TimeSpan.Zero;
            return true;
        }

        var roundStart = _roundStartedAt ?? now;
        unlockTime = roundStart.AddHours((lockDays - 1) * 24);

        if (now >= unlockTime)
        {
            remaining = TimeSpan.Zero;
            return true;
        }

        remaining = unlockTime - now;
        return false;
    }

    public bool AreFactionSectorsUnlocked()
    {
        return IsWarDeclarationUnlocked(DateTimeOffset.Now, out _, out _);
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var totalHours = (int) remaining.TotalHours;
        return $"{totalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    private string GetPrimeTimeText(string locId, DateTimeOffset now)
    {
        var (start, end) = GetPrimeTimeDisplayRange(now);
        return Loc.GetString(locId, ("start", start), ("end", end), ("timezone", MoscowTimeZoneLabel));
    }

    private (string Start, string End) GetPrimeTimeDisplayRange(DateTimeOffset now)
    {
        var startHour = NormalizeHour(_cfg.GetCVar(CLVars.FactionWarPrimeStartHour));
        var endHour = NormalizeHour(_cfg.GetCVar(CLVars.FactionWarPrimeEndHour));

        var start = new DateTimeOffset(now.Year, now.Month, now.Day, startHour, 0, 0, now.Offset).ToOffset(MoscowOffset);
        var end = new DateTimeOffset(now.Year, now.Month, now.Day, endHour, 0, 0, now.Offset).ToOffset(MoscowOffset);

        return ($"{start.Hour:D2}:{start.Minute:D2}", $"{end.Hour:D2}:{end.Minute:D2}");
    }

    private DateTimeOffset GetWarEndTime(DateTimeOffset now)
    {
        var endHour = NormalizeHour(_cfg.GetCVar(CLVars.FactionWarPrimeEndHour));
        var startHour = NormalizeHour(_cfg.GetCVar(CLVars.FactionWarPrimeStartHour));
        var endDate = new DateTimeOffset(now.Year, now.Month, now.Day, endHour, 0, 0, now.Offset);

        if (startHour == endHour)
            return now.AddHours(24);

        if (startHour < endHour)
            return endDate <= now ? endDate.AddDays(1) : endDate;

        return now.Hour >= startHour ? endDate.AddDays(1) : endDate;
    }

    private void UpdateWarPrimeStationProtection(DateTimeOffset now)
    {
        var shouldSuppressProtection = IsPrimeTime(now);
        if (_warPrimeProtectionSuppressed == shouldSuppressProtection)
            return;

        _warPrimeProtectionSuppressed = shouldSuppressProtection;

        var stationQuery = EntityQueryEnumerator<StationDataComponent>();
        while (stationQuery.MoveNext(out _, out var stationData))
        {
            foreach (var grid in stationData.Grids)
            {
                if (TerminatingOrDeleted(grid))
                    continue;

                if (shouldSuppressProtection)
                    SuppressGridProtection(grid);
                else
                    RestoreGridProtection(grid);
            }
        }
    }

    private void SuppressGridProtection(EntityUid grid)
    {
        if (!_suppressedProtectedGrids.ContainsKey(grid)
            && TryComp<ProtectedGridComponent>(grid, out var protectedGrid))
        {
            _suppressedProtectedGrids[grid] = new ProtectedGridState(
                protectedGrid.PreventFloorRemoval,
                protectedGrid.PreventFloorPlacement,
                protectedGrid.PreventRCDUse,
                protectedGrid.PreventEmpEvents,
                protectedGrid.PreventExplosions,
                protectedGrid.PreventArtifactTriggers,
                protectedGrid.KillHostileMobs);

            _protectedGrid.SetProtectionState(grid, false, false, false, false, false, false, false);
        }

        if (HasComp<GridGodModeComponent>(grid) && _suppressedGridGodMode.Add(grid))
            RemComp<GridGodModeComponent>(grid);
    }

    private void RestoreGridProtection(EntityUid grid)
    {
        if (_suppressedProtectedGrids.Remove(grid, out var protectedState))
        {
            _protectedGrid.SetProtectionState(
                grid,
                protectedState.PreventFloorRemoval,
                protectedState.PreventFloorPlacement,
                protectedState.PreventRcdUse,
                protectedState.PreventEmpEvents,
                protectedState.PreventExplosions,
                protectedState.PreventArtifactTriggers,
                protectedState.KillHostileMobs);
        }

        if (_suppressedGridGodMode.Remove(grid) && !HasComp<GridGodModeComponent>(grid))
            EnsureComp<GridGodModeComponent>(grid);
    }

    private void InvalidateCompany(string companyId)
    {
        RaiseNetworkEvent(new CompanyMembersInvalidateEvent(companyId), Filter.Empty().AddAllPlayers(_players));
    }

    public string GetDisplayName(string companyId)
    {
        return GetCompanyDisplayName(companyId);
    }

    private string GetCompanyDisplayName(string companyId)
    {
        return _prototypes.TryIndex<CompanyPrototype>(companyId, out var proto)
            ? proto.Name
            : companyId;
    }

    private string GetDeclarerName(ICommonSession session)
    {
        if (session.AttachedEntity is { } entity)
            return MetaData(entity).EntityName;

        return session.Name;
    }

    private static int NormalizeHour(int hour)
    {
        hour %= 24;
        if (hour < 0)
            hour += 24;

        return hour;
    }

    private sealed class ActiveFactionWar
    {
        public int Id { get; }
        public string AggressorCompanyId { get; }
        public string DefenderCompanyId { get; }
        public string DeclaredBy { get; }
        public string AnnouncementText { get; }
        public DateTimeOffset StartTime { get; }
        public DateTimeOffset EndTime { get; }
        public HashSet<string> PeaceRequests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ActiveFactionWar(int id, string aggressorCompanyId, string defenderCompanyId, string declaredBy, string announcementText, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            Id = id;
            AggressorCompanyId = aggressorCompanyId;
            DefenderCompanyId = defenderCompanyId;
            DeclaredBy = declaredBy;
            AnnouncementText = announcementText;
            StartTime = startTime;
            EndTime = endTime;
        }
    }

    private readonly record struct ProtectedGridState(
        bool PreventFloorRemoval,
        bool PreventFloorPlacement,
        bool PreventRcdUse,
        bool PreventEmpEvents,
        bool PreventExplosions,
        bool PreventArtifactTriggers,
        bool KillHostileMobs);
}
