// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._RMC14.Announce;
using Robust.Shared.Configuration;

namespace Content.Shared._Lua.CCVar;

[CVarDefs]
public sealed class LuaCCVars
{
    public static readonly CVarDef<AnnouncementDisplayPreference> AnnouncementStyle =
        CVarDef.Create("lua.announcement_style", AnnouncementDisplayPreference.Stylized, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> AnnouncementMaxVisible =
        CVarDef.Create("lua.announcement_max_visible", 2, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<string> AnnouncementStyleOverrides =
        CVarDef.Create("lua.announcement_style_overrides", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<string> AnnouncementLayoutOverrides =
        CVarDef.Create("lua.announcement_layout_overrides", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> AnnouncementMirrorChat =
        CVarDef.Create("lua.announcement_mirror_chat", false, CVar.ARCHIVE | CVar.CLIENTONLY);
}
