// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.AlertLevel;
using Content.Server.EUI;
using Content.Server._Mono.Company;
using Content.Server._NF.SectorServices;
using Content.Shared._Lua.Company;
using Content.Shared._Mono.Company;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Text;

namespace Content.Server._Lua.Company;

public sealed class CompanyBriefingSystem : EntitySystem
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly FactionWarSystem _wars = default!;
    [Dependency] private readonly CompanyMotdSystem _motds = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;

    private readonly Dictionary<ICommonSession, CompanyBriefingEui> _briefingUis = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CompanyComponent, CompanySetEvent>(OnCompanySet);
    }

    private void OnCompanySet(Entity<CompanyComponent> ent, ref CompanySetEvent args)
    {
        if (string.IsNullOrWhiteSpace(args.NewCompanyId) || string.Equals(args.NewCompanyId, "None", StringComparison.OrdinalIgnoreCase))
            return;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        if (!_prototypes.TryIndex<CompanyPrototype>(args.NewCompanyId, out var prototype))
            return;

        var briefing = BuildBriefing(args.NewCompanyId);
        if (string.IsNullOrWhiteSpace(briefing))
            return;

        OpenBriefingPopup(actor.PlayerSession, prototype.Name, prototype.Color, briefing);
    }

    private string BuildBriefing(string companyId)
    {
        var builder = new StringBuilder();

        AppendSection(builder, BuildCompanyIntro(companyId));
        AppendSection(builder, BuildLeaderMotd(companyId));

        AppendSection(builder, TryBuildAlertLevelBriefing());
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

        return Loc.GetString("company-briefing-company-info", ("text", prototype.Motd));
    }

    private string BuildLeaderMotd(string companyId)
    {
        var motd = _motds.GetMotd(companyId);
        if (string.IsNullOrWhiteSpace(motd))
            return string.Empty;

        return Loc.GetString("company-briefing-leader-motd", ("text", motd));
    }

    private string TryBuildAlertLevelBriefing()
    {
        if (!TryComp<AlertLevelComponent>(_sectorService.GetServiceEntity(), out var alert)
            || string.IsNullOrWhiteSpace(alert.CurrentLevel))
        {
            return string.Empty;
        }

        var level = alert.CurrentLevel;
        var levelName = Loc.TryGetString($"alert-level-{level}", out var localizedName)
            ? localizedName
            : level;
        var instructions = Loc.TryGetString($"alert-level-{level}-instructions", out var localizedInstructions)
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

    private void OpenBriefingPopup(ICommonSession session, string title, Color color, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (_briefingUis.Remove(session, out var existing))
            existing.Close();

        var eui = new CompanyBriefingEui(title, color, text, OnBriefingClosed);
        _briefingUis[session] = eui;
        _euiManager.OpenEui(eui, session);
    }

    private void OnBriefingClosed(ICommonSession session, CompanyBriefingEui eui)
    {
        if (_briefingUis.TryGetValue(session, out var current) && ReferenceEquals(current, eui))
            _briefingUis.Remove(session);
    }
}
