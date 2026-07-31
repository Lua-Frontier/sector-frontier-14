// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Company.Components;
using Content.Server._Mono.Company;
using Content.Server.Chat.Systems;
using Content.Server.Station.Components;
using Content.Shared.Administration.Logs;
using Content.Server.Station.Systems;
using Content.Shared._Lua.Company;
using Content.Shared.Lua.CLVar;
using Content.Shared.Mobs.Systems;
using Content.Shared._Mono.Company;
using Content.Shared.Database;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Company;

public sealed class FactionCaptureSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FactionOwnedStationSystem _ownedStations = default!;
    [Dependency] private readonly StationSystem _stations = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly FactionWarSystem _wars = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    private readonly HashSet<Entity<ActorComponent>> _nearbyActors = new();
    private readonly HashSet<EntityUid> _zonedStations = new();
    private readonly Dictionary<EntityUid, CaptureHudTracker> _captureHudTrackers = new();
    private readonly Dictionary<string, int> _participantCompanies = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ICommonSession> _hudRecipients = new();
    private const float CaptureUpdateIntervalSeconds = 0.5f;
    private static readonly TimeSpan CaptureHudInterval = TimeSpan.FromSeconds(0.5f);
    private const LookupFlags ActorLookupFlags = LookupFlags.Dynamic | LookupFlags.Approximate;
    private float _captureUpdateAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FactionOwnedStationComponent, ComponentStartup>(OnOwnedStationStartup);
        SubscribeLocalEvent<FactionCaptureComponent, ComponentStartup>(OnCaptureStartup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _captureUpdateAccumulator += frameTime;
        if (_captureUpdateAccumulator < CaptureUpdateIntervalSeconds)
            return;

        var captureFrameTime = _captureUpdateAccumulator;
        _captureUpdateAccumulator = 0f;

        _zonedStations.Clear();

        var zones = EntityQueryEnumerator<FactionCaptureZoneComponent, TransformComponent>();
        while (zones.MoveNext(out var zoneUid, out var zone, out var zoneXform))
        {
            var station = _stations.GetOwningStation(zoneUid);
            if (station == null)
                continue;

            if (!TryComp<FactionCaptureComponent>(station.Value, out var capture)
                || !TryComp<FactionOwnedStationComponent>(station.Value, out var owned))
            {
                continue;
            }

            _zonedStations.Add(station.Value);
            CollectActorsInRange(zoneXform.Coordinates, zone.CaptureRadius ?? capture.CaptureRadius);
            UpdateCapture(station.Value, capture, owned, captureFrameTime, zone);
        }

        var stationQuery = EntityQueryEnumerator<FactionCaptureComponent, FactionOwnedStationComponent>();
        while (stationQuery.MoveNext(out var uid, out var capture, out var owned))
        {
            if (_zonedStations.Contains(uid) || !capture.CaptureWholeStation) continue;
            CollectActorsOnStation(uid);
            UpdateCapture(uid, capture, owned, captureFrameTime, null);
        }
    }

    public void ResetCaptureState(EntityUid station)
    {
        if (!TryComp<FactionCaptureComponent>(station, out var capture))
            return;

        if (!string.IsNullOrWhiteSpace(capture.AttackingCompany) &&
            _ownedStations.TryGetCurrentOwner(station, out var ownerCompany))
        {
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"Capture progress reset on station {Name(station)}. Attacker: {_wars.GetDisplayName(capture.AttackingCompany)}. Defender: {_wars.GetDisplayName(ownerCompany!)}");
        }

        ResetCapture(station, capture);
    }

    private void OnOwnedStationStartup(EntityUid uid, FactionOwnedStationComponent component, ComponentStartup args)
    {
        var capture = EnsureComp<FactionCaptureComponent>(uid);
        InitializeCapture(uid, capture);
    }

    private void OnCaptureStartup(EntityUid uid, FactionCaptureComponent component, ComponentStartup args)
    {
        EnsureComp<FactionOwnedStationComponent>(uid);
        InitializeCapture(uid, component);
    }

    private void InitializeCapture(EntityUid uid, FactionCaptureComponent capture)
    {
        if (capture.RequiredAttackers <= 0)
            capture.RequiredAttackers = _cfg.GetCVar(CLVars.FactionWarCaptureRequiredAttackers);

        if (capture.CaptureDuration <= 0f)
            capture.CaptureDuration = _cfg.GetCVar(CLVars.FactionWarCaptureDurationSeconds);
    }

    private void CollectActorsInRange(EntityCoordinates center, float radius)
    {
        _nearbyActors.Clear();
        if (radius <= 0f) return;
        _lookup.GetEntitiesInRange(center, radius, _nearbyActors, ActorLookupFlags);
    }

    private void CollectActorsOnStation(EntityUid station)
    {
        _nearbyActors.Clear();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var actor, out var xform))
        {
            if (_stations.GetOwningStation(uid, xform) != station) continue;
            _nearbyActors.Add((uid, actor));
        }
    }

    private void UpdateCapture(EntityUid station, FactionCaptureComponent capture, FactionOwnedStationComponent owned, float frameTime, FactionCaptureZoneComponent? zone)
    {
        if (!owned.CanBeCaptured)
        {
            ResetCapture(station, capture);
            return;
        }

        _ownedStations.TryGetCurrentOwner(station, out var ownerCompany);
        var isUnownedStation = string.IsNullOrWhiteSpace(ownerCompany);

        string? attackerCompany;
        if (isUnownedStation)
        {
            attackerCompany = GetNeutralCaptureAttacker(capture.AttackingCompany);
        }
        else if (!TryGetWarCaptureAttacker(ownerCompany!, capture.AttackingCompany, out attackerCompany)
                 || string.IsNullOrWhiteSpace(attackerCompany))
        {
            ResetCapture(station, capture);
            return;
        }

        if (string.IsNullOrWhiteSpace(attackerCompany))
        {
            ResetCapture(station, capture);
            return;
        }
        var requiredAttackers = zone?.RequiredAttackers ?? capture.RequiredAttackers;
        var captureDuration = zone?.CaptureDuration ?? capture.CaptureDuration;
        var resetOnDefenderPresence = zone?.ResetOnDefenderPresence ?? capture.ResetOnDefenderPresence;
        var pausedIfNoAttackers = zone?.PausedIfNoAttackers ?? capture.PausedIfNoAttackers;

        var counts = CountParticipants(attackerCompany, ownerCompany);

        if (resetOnDefenderPresence && counts.defenders > 0)
        {
            PublishCaptureHud(
                station,
                new CompanyCaptureStatusEvent(
                    true,
                    Name(station),
                    _wars.GetDisplayName(capture.AttackingCompany ?? attackerCompany),
                    GetDefenderDisplayName(ownerCompany),
                    captureDuration <= 0f ? 0f : Math.Clamp(capture.Progress / captureDuration, 0f, 1f),
                    counts.attackers,
                    requiredAttackers,
                    counts.defenders,
                    true));
            return;
        }

        if (counts.attackers < requiredAttackers)
        {
            if (!pausedIfNoAttackers)
            {
                ResetCapture(station, capture);
                return;
            }

            PublishCaptureHud(
                station,
                new CompanyCaptureStatusEvent(
                    true,
                    Name(station),
                    _wars.GetDisplayName(capture.AttackingCompany ?? attackerCompany),
                    GetDefenderDisplayName(ownerCompany),
                    captureDuration <= 0f ? 0f : Math.Clamp(capture.Progress / captureDuration, 0f, 1f),
                    counts.attackers,
                    requiredAttackers,
                    counts.defenders,
                    true));
            return;
        }

        if (!string.Equals(capture.AttackingCompany, attackerCompany, StringComparison.OrdinalIgnoreCase))
        {
            var previousAttacker = capture.AttackingCompany;
            capture.AttackingCompany = attackerCompany;
            capture.Progress = 0f;

            if (previousAttacker == null)
            {
                _adminLogger.Add(LogType.Action, LogImpact.Medium,
                    $"Capture started on station {Name(station)} by {_wars.GetDisplayName(attackerCompany)} against {GetDefenderDisplayName(ownerCompany)}");
            }
            else
            {
                _adminLogger.Add(LogType.Action, LogImpact.Medium,
                    $"Capture attacker changed on station {Name(station)}: {_wars.GetDisplayName(previousAttacker)} -> {_wars.GetDisplayName(attackerCompany)} against {GetDefenderDisplayName(ownerCompany)}");
            }
        }

        capture.Progress += frameTime;

        if (capture.Progress < captureDuration)
        {
            PublishCaptureHud(
                station,
                new CompanyCaptureStatusEvent(
                    true,
                    Name(station),
                    _wars.GetDisplayName(attackerCompany),
                    GetDefenderDisplayName(ownerCompany),
                    captureDuration <= 0f ? 1f : Math.Clamp(capture.Progress / captureDuration, 0f, 1f),
                    counts.attackers,
                    requiredAttackers,
                    counts.defenders,
                    false));
            return;
        }

        CompleteCapture(station, capture, owned, attackerCompany, ownerCompany);
    }

    private (int attackers, int defenders) CountParticipants(string attackerCompany, string? defenderCompany)
    {
        var attackers = 0;
        var defenders = 0;

        foreach (var (entity, _) in _nearbyActors)
        {
            if (!TryComp<CompanyComponent>(entity, out var company))
                continue;

            if (_mobState.IsDead(entity) || _mobState.IsIncapacitated(entity))
                continue;

            if (string.Equals(company.CompanyName, attackerCompany, StringComparison.OrdinalIgnoreCase))
            {
                attackers++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(defenderCompany)
                && string.Equals(company.CompanyName, defenderCompany, StringComparison.OrdinalIgnoreCase))
                defenders++;
        }

        return (attackers, defenders);
    }

    private string? GetNeutralCaptureAttacker(string? preferredCompany)
    {
        _participantCompanies.Clear();

        foreach (var (entity, _) in _nearbyActors)
        {
            if (!TryComp<CompanyComponent>(entity, out var company))
                continue;

            if (_mobState.IsDead(entity) || _mobState.IsIncapacitated(entity))
                continue;

            if (string.IsNullOrWhiteSpace(company.CompanyName)
                || string.Equals(company.CompanyName, "None", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _participantCompanies.TryGetValue(company.CompanyName, out var count);
            _participantCompanies[company.CompanyName] = count + 1;
        }

        if (!string.IsNullOrWhiteSpace(preferredCompany)
            && _participantCompanies.TryGetValue(preferredCompany, out var preferredCount)
            && preferredCount > 0)
        {
            return preferredCompany;
        }

        string? selected = null;
        var highestCount = 0;

        foreach (var (companyId, count) in _participantCompanies)
        {
            if (count <= highestCount)
                continue;

            highestCount = count;
            selected = companyId;
        }

        return selected;
    }

    private bool TryGetWarCaptureAttacker(string defendingCompanyId, string? preferredCompany, out string? attackerCompany)
    {
        attackerCompany = null;
        _participantCompanies.Clear();

        foreach (var (entity, _) in _nearbyActors)
        {
            if (!TryComp<CompanyComponent>(entity, out var company))
                continue;

            if (_mobState.IsDead(entity) || _mobState.IsIncapacitated(entity))
                continue;

            if (string.IsNullOrWhiteSpace(company.CompanyName)
                || string.Equals(company.CompanyName, defendingCompanyId, StringComparison.OrdinalIgnoreCase)
                || !_wars.TryGetWarBetween(defendingCompanyId, company.CompanyName, out _))
            {
                continue;
            }

            _participantCompanies.TryGetValue(company.CompanyName, out var count);
            _participantCompanies[company.CompanyName] = count + 1;
        }

        if (!string.IsNullOrWhiteSpace(preferredCompany)
            && _participantCompanies.TryGetValue(preferredCompany, out var preferredCount)
            && preferredCount > 0)
        {
            attackerCompany = preferredCompany;
            return true;
        }

        var highestCount = 0;
        foreach (var (companyId, count) in _participantCompanies)
        {
            if (count <= highestCount)
                continue;

            highestCount = count;
            attackerCompany = companyId;
        }

        return attackerCompany != null;
    }

    private void CompleteCapture(EntityUid station, FactionCaptureComponent capture, FactionOwnedStationComponent owned, string attackerCompany, string? defenderCompany)
    {
        _ownedStations.SetOwner(station, attackerCompany, owned);

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"Station {Name(station)} captured by {_wars.GetDisplayName(attackerCompany)} from {GetDefenderDisplayName(defenderCompany)}");

        var message = string.IsNullOrWhiteSpace(defenderCompany)
            ? Loc.GetString(
                "company-capture-success-global-unowned",
                ("attacker", _wars.GetDisplayName(attackerCompany)),
                ("station", Name(station)))
            : Loc.GetString(
                "company-capture-success-global",
                ("attacker", _wars.GetDisplayName(attackerCompany)),
                ("defender", GetDefenderDisplayName(defenderCompany)),
                ("station", Name(station)));

        _chat.DispatchGlobalAnnouncement(
            message,
            Loc.GetString("company-capture-announcement-title"),
            true,
            new SoundPathSpecifier("/Audio/_Lua/Alarm/warmessage.ogg"),
            colorOverride: Color.OrangeRed);

        ResetCapture(station, capture);
    }

    private string GetDefenderDisplayName(string? defenderCompany)
    {
        return string.IsNullOrWhiteSpace(defenderCompany)
            ? Loc.GetString("company-capture-unowned")
            : _wars.GetDisplayName(defenderCompany);
    }

    private void ResetCapture(EntityUid station, FactionCaptureComponent capture)
    {
        if (capture.Progress == 0f && string.IsNullOrWhiteSpace(capture.AttackingCompany))
        {
            ClearCaptureHud(station);
            return;
        }

        capture.Progress = 0f;
        capture.AttackingCompany = null;
        ClearCaptureHud(station);
    }

    private void PublishCaptureHud(EntityUid station, CompanyCaptureStatusEvent state)
    {
        FillCaptureHudRecipients();

        if (!_captureHudTrackers.TryGetValue(station, out var tracker))
        {
            tracker = new CaptureHudTracker();
            _captureHudTrackers[station] = tracker;
        }

        if (_hudRecipients.Count == 0)
        {
            ClearCaptureHud(station);
            return;
        }

        if (_timing.CurTime < tracker.NextUpdate && tracker.Recipients.SetEquals(_hudRecipients))
            return;

        RaiseNetworkEvent(state, Filter.Empty().AddPlayers(_hudRecipients));

        foreach (var stale in tracker.Recipients)
        {
            if (_hudRecipients.Contains(stale))
                continue;

            RaiseNetworkEvent(new CompanyCaptureStatusEvent(false), Filter.SinglePlayer(stale));
        }

        tracker.Recipients.Clear();
        foreach (var recipient in _hudRecipients)
            tracker.Recipients.Add(recipient);

        tracker.NextUpdate = _timing.CurTime + CaptureHudInterval;
    }

    private void ClearCaptureHud(EntityUid station)
    {
        if (!_captureHudTrackers.Remove(station, out var tracker))
            return;

        foreach (var recipient in tracker.Recipients)
            RaiseNetworkEvent(new CompanyCaptureStatusEvent(false), Filter.SinglePlayer(recipient));
    }

    private void FillCaptureHudRecipients()
    {
        _hudRecipients.Clear();
        foreach (var (_, actor) in _nearbyActors)
            _hudRecipients.Add(actor.PlayerSession);
    }

    private sealed class CaptureHudTracker
    {
        public HashSet<ICommonSession> Recipients { get; } = new();
        public TimeSpan NextUpdate;
    }
}
