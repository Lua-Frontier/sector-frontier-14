// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Client._Lua.AmmoLoader.UI;

public static class AmmoLoaderLocale
{
    private const string TypeAttr = "ammo-loader-type";
    private const string CaliberAttr = "ammo-loader-caliber";
    private const string WeightAttr = "ammo-loader-weight";

    private static ILocalizationManager Localization => IoCManager.Resolve<ILocalizationManager>();

    public static string GetAmmoType(string prototypeId)
    {
        if (TryGetEntityAttribute(prototypeId, TypeAttr, out var value)) return value;
        return Loc.GetString("ammo-loader-item-type-default");
    }

    public static string GetAmmoCaliber(string prototypeId, string fallbackName)
    {
        if (TryGetEntityAttribute(prototypeId, CaliberAttr, out var value)) return value;
        return fallbackName;
    }

    public static string GetAmmoWeight(string prototypeId)
    {
        if (TryGetEntityAttribute(prototypeId, WeightAttr, out var value)) return value;
        return Loc.GetString("ammo-loader-item-weight-unknown");
    }

    public static string FormatItemStats(string prototypeId, string displayName)
    {
        var type = GetAmmoType(prototypeId);
        var caliber = GetAmmoCaliber(prototypeId, displayName);
        var weight = GetAmmoWeight(prototypeId);
        var typeLine = Loc.GetString("ammo-loader-item-stats-type", ("value", type));
        var caliberLine = Loc.GetString("ammo-loader-item-stats-caliber", ("value", caliber));
        var weightLine = Loc.GetString("ammo-loader-item-stats-weight", ("value", weight));
        return typeLine + '\n' + caliberLine + '\n' + weightLine;
    }

    private static bool TryGetEntityAttribute(string prototypeId, string attribute, out string value)
    {
        if (Localization.GetEntityData(prototypeId).Attributes.TryGetValue(attribute, out var attrValue) && !string.IsNullOrWhiteSpace(attrValue))
        {
            value = attrValue;
            return true;
        }
        value = string.Empty;
        return false;
    }
}
