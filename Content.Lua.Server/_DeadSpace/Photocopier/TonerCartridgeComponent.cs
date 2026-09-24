// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
//This content is sourced from Мёртвый Космос and is used with explicit permission for use in Sector Frontier(LuaWorld) https://github.com/HacksLua/sector-frontier-14.
// Мёртвый Космос - This file is licensed under AGPLv3
// Copyright (c) 2025 Мёртвый Космос Contributors
// See AGPLv3.txt for details.

namespace Content.Lua.Server._DeadSpace.Photocopier;

[RegisterComponent]
public sealed partial class TonerCartridgeComponent : Component
{
    [DataField("maxAmount")]
    public int MaxAmount = 30;

    [DataField("currentAmount")]
    public int CurrentAmount = 30;
}
