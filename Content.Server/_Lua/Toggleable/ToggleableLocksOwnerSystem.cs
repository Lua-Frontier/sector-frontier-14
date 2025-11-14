// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared._Lua.Toggleable;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Components;
using Robust.Shared.Containers;

namespace Content.Server._Lua.Toggleable;

public sealed class ToggleableLocksOwnerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToggleableLocksOwnerComponent, EntRemovedFromContainerMessage>(OnRemovedFromContainer);
        SubscribeLocalEvent<ToggleableLocksOwnerComponent, EntInsertedIntoContainerMessage>(OnInsertedIntoContainer);
    }

    private void OnRemovedFromContainer(EntityUid owner, ToggleableLocksOwnerComponent locker, ref EntRemovedFromContainerMessage args)
    {
        var removed = args.Entity;
        if (TerminatingOrDeleted(owner)) return;
        if (!IsWhitelisted(removed, locker)) return;
        locker.ActiveLocks++;
        if (locker.ActiveLocks == 1) EnsureComp<UnremoveableComponent>(owner);
    }

    private void OnInsertedIntoContainer(EntityUid owner, ToggleableLocksOwnerComponent locker, ref EntInsertedIntoContainerMessage args)
    {
        var inserted = args.Entity;
        if (TerminatingOrDeleted(owner)) return;
        if (!IsWhitelisted(inserted, locker)) return;
        if (locker.ActiveLocks > 0) locker.ActiveLocks--;
        if (locker.ActiveLocks == 0 && HasComp<UnremoveableComponent>(owner)) RemComp<UnremoveableComponent>(owner);
    }

    private bool IsWhitelisted(EntityUid clothingUid, ToggleableLocksOwnerComponent locker)
    {
        if (locker.ClothingPrototypes.Count == 0) return false;
        if (!TryComp<MetaDataComponent>(clothingUid, out var meta) || meta.EntityPrototype == null) return false;
        return locker.ClothingPrototypes.Contains(meta.EntityPrototype.ID);
    }
}


