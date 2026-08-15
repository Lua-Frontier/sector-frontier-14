// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System;

namespace Content.Shared._Lua.Starmap;

public static class SectorVisibility
{
    public const string NoneCompany = "None";

    public static string NormalizeCompanyId(string? companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId) ||
            string.Equals(companyId, NoneCompany, StringComparison.OrdinalIgnoreCase))
            return NoneCompany;

        return companyId;
    }

    public static bool IsSectorVisible(StarDefinition def, string? companyId, bool globallyUnlocked)
    {
        return IsSectorVisible(def, companyId, globallyUnlocked, null);
    }

    public static bool IsSectorVisible(
        StarDefinition def,
        string? companyId,
        bool globallyUnlocked,
        IReadOnlyCollection<string>? learned)
    {
        var company = NormalizeCompanyId(companyId);

        if (learned != null)
        {
            foreach (var sectorId in learned)
            {
                if (string.Equals(sectorId, def.Id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        if (globallyUnlocked)
        {
            if (!def.ExcludeFromGlobalUnlock)
                return true;

            return IsCompanyListed(def, company) || def.VisibleToAll;
        }

        if (def.VisibleToAll)
            return true;

        return IsCompanyListed(def, company);
    }

    public static bool IsSectorVisible(
        ComposedStarmapData data,
        string sectorId,
        string? companyId,
        bool globallyUnlocked)
    {
        return IsSectorVisible(data, sectorId, companyId, globallyUnlocked, null);
    }

    public static bool IsSectorVisible(
        ComposedStarmapData data,
        string sectorId,
        string? companyId,
        bool globallyUnlocked,
        IReadOnlyCollection<string>? learned)
    {
        foreach (var def in data.Stars)
        {
            if (!string.Equals(def.Id, sectorId, StringComparison.OrdinalIgnoreCase))
                continue;

            return IsSectorVisible(def, companyId, globallyUnlocked, learned);
        }

        return globallyUnlocked;
    }

    [Obsolete("Use ComposedStarmapData overload")]
    public static bool IsSectorVisible(
        StarmapDataPrototype data,
        string sectorId,
        string? companyId,
        bool globallyUnlocked)
    {
        return IsSectorVisible(data, sectorId, companyId, globallyUnlocked, null);
    }

    [Obsolete("Use ComposedStarmapData overload")]
    public static bool IsSectorVisible(
        StarmapDataPrototype data,
        string sectorId,
        string? companyId,
        bool globallyUnlocked,
        IReadOnlyCollection<string>? learned)
    {
        foreach (var def in data.Stars)
        {
            if (!string.Equals(def.Id, sectorId, StringComparison.OrdinalIgnoreCase))
                continue;

            return IsSectorVisible(def, companyId, globallyUnlocked, learned);
        }

        return globallyUnlocked;
    }

    private static bool IsCompanyListed(StarDefinition def, string company)
    {
        if (def.VisibleCompanies.Length == 0)
            return false;

        foreach (var listed in def.VisibleCompanies)
        {
            if (string.Equals(listed, company, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
