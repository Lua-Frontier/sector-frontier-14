// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client.Guidebook.Richtext;
using Content.Shared.Lua.CLVar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using System.Diagnostics.CodeAnalysis;

namespace Content.Client._Lua.Guidebook;

/// <summary>
/// Guidebook paragraph for station payout rules, driven by lua.station_payout.* CVars.
/// </summary>
[UsedImplicitly]
public sealed class GuideStationPayoutText : IDocumentTag
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public GuideStationPayoutText()
    {
        IoCManager.InjectDependencies(this);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        var intervalSeconds = Math.Max(1, _cfg.GetCVar(CLVars.StationPayoutIntervalSeconds));
        var perStation = Math.Max(0, _cfg.GetCVar(CLVars.StationPayoutPerStation));

        var label = new RichTextLabel
        {
            HorizontalExpand = true,
        };
        label.SetMessage(Loc.GetString("starmap-payout-guide-text",
            ("interval", FormatInterval(intervalSeconds)),
            ("amount", perStation)));

        control = label;
        return true;
    }

    private static string FormatInterval(int seconds)
    {
        if (seconds >= 3600 && seconds % 3600 == 0)
        {
            var hours = seconds / 3600;
            return hours == 1
                ? Loc.GetString("payout-interval-hour")
                : Loc.GetString("payout-interval-hours", ("hours", hours));
        }

        if (seconds >= 60 && seconds % 60 == 0)
            return Loc.GetString("payout-interval-minutes", ("minutes", seconds / 60));

        return Loc.GetString("payout-interval-seconds", ("seconds", seconds));
    }
}
