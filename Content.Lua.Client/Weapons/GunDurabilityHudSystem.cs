// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Items;
using Content.Lua.Shared.Weapons;
using Robust.Shared.Timing;

namespace Content.Client.Lua.Weapons;

public sealed class GunDurabilityHudSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<GunJamComponent>(ent => new GunDurabilityStatusControl(ent, _entityManager, _timing));
    }
}
