// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Server.GameObjects;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._Lua.Tools.Components;
using Content.Shared._NF.Shipyard.Components;

namespace Content.Server._Lua.Tools.Systems;

public sealed class ShuttleTabletSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ShuttleTabletComponent, TransformComponent>();

        while (query.MoveNext(out var entity, out _, out var transformComp))
        {
            var card = _slots.GetItemOrNull(entity, "id_container");

            if (card == null)
            {
                continue;
            }

            if (!TryComp<ShuttleDeedComponent>(card, out var deedComp))
            {
                continue;
            }

            _transformSystem.SetGridId(entity, transformComp, deedComp.ShuttleUid);
        }
    }
}
