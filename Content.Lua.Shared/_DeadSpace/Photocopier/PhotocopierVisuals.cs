// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
//This content is sourced from Мёртвый Космос and is used with explicit permission for use in Sector Frontier(LuaWorld) https://github.com/HacksLua/sector-frontier-14.
// Мёртвый Космос - This file is licensed under AGPLv3
// Copyright (c) 2025 Мёртвый Космос Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Lua.Shared._DeadSpace.Photocopier;

[Serializable, NetSerializable]
public enum PhotocopierVisuals : byte
{
    VisualState,
}

[Serializable, NetSerializable]
public enum PhotocopierVisualState : byte
{
    Normal,
    Scanning,
    Printing
}
