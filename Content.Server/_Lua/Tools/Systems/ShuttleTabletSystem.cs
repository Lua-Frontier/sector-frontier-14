// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Server.GameObjects;
using Content.Shared._NF.GridAccess;
using Content.Shared._Lua.Tools.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._NF.Shipyard.Components;
using Content.Server.Shuttles.Components;

namespace Content.Server._Lua.Tools.Systems;

public sealed class ShuttleTabletSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleTabletComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShuttleTabletComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(EntityUid uid, ShuttleTabletComponent component, ComponentInit args)
    {
        EnsureComp<GridAccessComponent>(uid);
        EnsureComp<ShuttleConsoleComponent>(uid);
        _slots.AddItemSlot(uid, "IDContainer", component.IDContainerSlot);
    }

    private void OnComponentRemove(EntityUid uid, ShuttleTabletComponent component, ComponentRemove args)
    {
        RemComp<GridAccessComponent>(uid);
        RemComp<ShuttleConsoleComponent>(uid);
        _slots.TryEjectToHands(uid, component.IDContainerSlot, null);
        _slots.RemoveItemSlot(uid, component.IDContainerSlot);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ShuttleTabletComponent, GridAccessComponent, TransformComponent>();

        while (query.MoveNext(out var entity, out _, out var gridAccessComp, out var transformComp))
        {
            var card = _slots.GetItemOrNull(entity, "IDContainer");

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
