// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.AlertLevel;
using Content.Server._Lua.Sectors;
using Content.Server._Mono.Company;
using Content.Server._NF.SectorServices;
using Content.Shared._Lua.Company;
using Content.Shared._Mono.Company;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Text;

namespace Content.Server._Lua.Company;

public sealed class CompanyBriefingSystem : EntitySystem
{
    [Dependency] private readonly FactionWarSystem _wars = default!;
    [Dependency] private readonly CompanyMotdSystem _motds = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;
    [Dependency] private readonly SectorSystem _sectorSystem = default!;

    private readonly HashSet<NetUserId> _shownBriefings = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CompanyComponent, CompanySetEvent>(OnCompanySet);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _shownBriefings.Clear());
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        _shownBriefings.Remove(ev.Player.UserId);
    }

    private void OnCompanySet(Entity<CompanyComponent> ent, ref CompanySetEvent args)
    {
        if (string.IsNullOrWhiteSpace(args.NewCompanyId) || string.Equals(args.NewCompanyId, "None", StringComparison.OrdinalIgnoreCase))
            return;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        if (!_shownBriefings.Add(actor.PlayerSession.UserId))
            return;

        if (!_prototypes.HasIndex<CompanyPrototype>(args.NewCompanyId))
            return;

        var briefing = BuildBriefing(args.NewCompanyId, ent);
        if (string.IsNullOrWhiteSpace(briefing))
        {
            _shownBriefings.Remove(actor.PlayerSession.UserId);
            return;
        }

        RaiseNetworkEvent(
            new CompanyBriefingOverlayMessage(briefing),
            Filter.SinglePlayer(actor.PlayerSession));
    }

    private string BuildBriefing(string companyId, EntityUid entity)
    {
        var builder = new StringBuilder();

        AppendSection(builder, BuildCompanyIntro(companyId));
        AppendSection(builder, BuildLeaderMotd(companyId));

        AppendSection(builder, TryBuildAlertLevelBriefing(entity));
        AppendSection(builder, BuildWarBriefing(companyId));

        return builder.ToString();
    }

    private string BuildCompanyIntro(string companyId)
    {
        if (!_prototypes.TryIndex<CompanyPrototype>(companyId, out var prototype)
            || string.IsNullOrWhiteSpace(prototype.Motd))
        {
            return string.Empty;
        }

        var text = Loc.TryGetString(prototype.Motd, out var localized) ? localized : prototype.Motd;
        return Loc.GetString("company-briefing-company-info", ("text", text));
    }

    private string BuildLeaderMotd(string companyId)
    {
        var motd = _motds.GetMotd(companyId);
        if (string.IsNullOrWhiteSpace(motd))
            return string.Empty;

        return Loc.GetString("company-briefing-leader-motd", ("text", motd));
    }

    private string TryBuildAlertLevelBriefing(EntityUid entity)
    {
        if (!_sectorService.TryGetServiceEntity(entity, out var service)
            || !TryComp<AlertLevelComponent>(service, out var alert)
            || string.IsNullOrWhiteSpace(alert.CurrentLevel))
        {
            return string.Empty;
        }

        var level = alert.CurrentLevel;
        var sectorName = Loc.GetString("alert-level-sector-unknown");
        if (TryComp(entity, out TransformComponent? xform) && xform.MapID != MapId.Nullspace)
            sectorName = _sectorSystem.GetSectorDisplayName(xform.MapID);

        var levelName = Loc.TryGetString($"alert-level-{level}", out var localizedName)
            ? localizedName
            : level;
        var instructionsKey = $"alert-level-{level}-instructions";
        var instructions = Loc.TryGetString(instructionsKey, out var localizedInstructions, ("sector", sectorName))
            ? localizedInstructions
            : Loc.GetString("alert-level-unknown-instructions");

        return Loc.GetString(
            "company-briefing-alert-level",
            ("level", levelName),
            ("instructions", instructions));
    }

    private string BuildWarBriefing(string companyId)
    {
        var wars = _wars.GetActiveWarOverviews(companyId);
        if (wars.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();

        foreach (var war in wars)
        {
            if (builder.Length > 0)
                builder.Append('\n').Append('\n');

            builder.Append(Loc.GetString(
                "company-war-briefing-ongoing",
                ("aggressor", war.AggressorName),
                ("defender", war.DefenderName),
                ("declaredBy", war.DeclaredBy),
                ("endTime", TimeSpan.FromSeconds(war.RemainingSeconds).ToString(@"hh\:mm\:ss")),
                ("message", war.AnnouncementText)));
        }

        return builder.ToString();
    }

    private static void AppendSection(StringBuilder builder, string section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return;

        if (builder.Length > 0)
            builder.Append('\n').Append('\n');

        builder.Append(section);
    }
}
