// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System;
using System.IO;
using Content.Shared._Mono.Company;
using Content.Shared.Access.Systems;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Announce;

public static class AnnouncementOverlayAppearanceHelper
{
    public static bool TryResolveCompanyIcon(CompanyPrototype company, out string rsi, out string state)
    {
        rsi = string.Empty;
        state = string.Empty;
        if (string.IsNullOrWhiteSpace(company.IconPath))
            return false;

        return TryParseIconPath(company.IconPath, out rsi, out state);
    }

    public static bool TryParseIconPath(string iconPath, out string rsi, out string state)
    {
        rsi = string.Empty;
        state = string.Empty;
        if (string.IsNullOrWhiteSpace(iconPath))
            return false;

        var normalized = iconPath.Trim().Replace('\\', '/');
        if (normalized.StartsWith("/Textures/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["/Textures/".Length..];
        else if (normalized.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["Textures/".Length..];

        var slash = normalized.LastIndexOf('/');
        if (slash < 0)
            return false;

        var file = normalized[(slash + 1)..];
        rsi = normalized[..slash];
        if (!rsi.EndsWith(".rsi", StringComparison.OrdinalIgnoreCase))
            return false;

        state = Path.GetFileNameWithoutExtension(file);
        if (string.IsNullOrWhiteSpace(state))
            return false;

        rsi = $"/Textures/{rsi.TrimStart('/')}";
        return true;
    }

    public static string? ResolveSpeakerJobTitle(IPrototypeManager prototypes, SharedIdCardSystem idCards, EntityUid speaker)
    {
        if (!idCards.TryFindIdCard(speaker, out var idCard))
            return null;

        var job = idCard.Comp.LocalizedJobTitle;
        if (string.IsNullOrWhiteSpace(job) && idCard.Comp.JobTitle is { } jobId)
            job = Loc.GetString(jobId);

        return string.IsNullOrWhiteSpace(job) ? null : job;
    }
}
