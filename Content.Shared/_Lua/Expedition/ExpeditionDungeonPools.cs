// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Expedition;

public static class ExpeditionDungeonPools
{
    public static readonly ProtoId<DungeonConfigPrototype>[] Shared =
    [
        "LuaMineshaft",
        "LuaOutpost",
        "LuaCaveFactory",
        "LuaExperiment",
        "LuaLavaBrig",
        "LuaHaunted",
        "LuaSnowyLabs",
        "LuaLavaMercenary",
        "LuaVirologyLab",
    ];

    public static readonly ProtoId<DungeonConfigPrototype>[] ExpeditionLarge = Shared;
    public static readonly ProtoId<DungeonConfigPrototype>[] ExpeditionGrass = Shared;
    public static readonly ProtoId<DungeonConfigPrototype>[] ExpeditionCaves = Shared;
    public static readonly ProtoId<DungeonConfigPrototype>[] ExpeditionShadow = Shared;
    public static readonly ProtoId<DungeonConfigPrototype>[] ExpeditionExtreme = Shared;
    public static readonly ProtoId<DungeonConfigPrototype>[] StargateLegacy = Shared;
}
