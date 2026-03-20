// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Items;
using Content.Shared._Lua.Weapons;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Lua.Weapons;

public sealed class GunDurabilityHudSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

    }
}
