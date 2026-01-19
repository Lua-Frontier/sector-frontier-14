// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Inventory;
using Content.Shared._Lua.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Lua.Clothing.Systems;

public sealed class HideInnerClothingSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly Dictionary<EntityUid, HashSet<string>> _hiddenKeys = new();
    private readonly HashSet<EntityUid> _pendingWearerUpdates = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_timing.ApplyingState || _pendingWearerUpdates.Count == 0) return;
        foreach (var wearer in _pendingWearerUpdates)
        { UpdateHiddenLayers(wearer); }
        _pendingWearerUpdates.Clear();
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HideInnerClothingComponent, ClothingGotEquippedEvent>(OnOuterClothingEquipped);
        SubscribeLocalEvent<HideInnerClothingComponent, ClothingGotUnequippedEvent>(OnOuterClothingUnequipped);
        SubscribeLocalEvent<ClothingComponent, EquipmentVisualsUpdatedEvent>(OnClothingVisualsUpdated);
    }

    private void OnOuterClothingEquipped(Entity<HideInnerClothingComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (_timing.ApplyingState)
        {
            _pendingWearerUpdates.Add(args.Wearer);
            return;
        }
        if (!TryComp<ClothingComponent>(ent.Owner, out var clothing)) return;
        var slotFlags = clothing.InSlotFlag ?? SlotFlags.NONE;
        if ((slotFlags & SlotFlags.OUTERCLOTHING) == 0) return;
        UpdateHiddenLayers(args.Wearer);
    }

    private void OnOuterClothingUnequipped(Entity<HideInnerClothingComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (_timing.ApplyingState)
        {
            _pendingWearerUpdates.Add(args.Wearer);
            return;
        }
        if (!TryComp<ClothingComponent>(ent.Owner, out var clothing)) return;
        if ((clothing.Slots & SlotFlags.OUTERCLOTHING) == 0) return;
        UpdateHiddenLayers(args.Wearer);
    }

    private void OnClothingVisualsUpdated(Entity<ClothingComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        if (_timing.ApplyingState)
        {
            _pendingWearerUpdates.Add(args.Equipee);
            return;
        }
        if (HasComp<HideInnerClothingComponent>(ent.Owner))
        {
            var outerFlags = ent.Comp.InSlotFlag ?? SlotFlags.NONE;
            if ((outerFlags & SlotFlags.OUTERCLOTHING) != 0) UpdateHiddenLayers(args.Equipee);
        }
        if (!HasOuterHiderEquipped(args.Equipee)) return;
        var inSlotFlag = ent.Comp.InSlotFlag ?? SlotFlags.NONE;
        if ((inSlotFlag & SlotFlags.INNERCLOTHING) == 0) return;
        if (!TryComp<SpriteComponent>(args.Equipee, out var sprite)) return;
        var hidden = GetOrCreateHiddenKeys(args.Equipee);
        foreach (var key in args.RevealedLayers)
        {
            if (_sprite.LayerMapTryGet((args.Equipee, sprite), key, out _, false))
            {
                _sprite.LayerSetVisible((args.Equipee, sprite), key, false);
                hidden.Add(key);
            }
        }
    }

    private void UpdateHiddenLayers(EntityUid wearer)
    {
        if (_timing.ApplyingState) return;
        if (!TryComp<SpriteComponent>(wearer, out var sprite)) return;
        if (!TryComp<InventorySlotsComponent>(wearer, out var inventorySlots)) return;
        var hasOuter = HasOuterHiderEquipped(wearer);
        if (hasOuter)
        {
            var hidden = GetOrCreateHiddenKeys(wearer);
            foreach (var (slot, layerKeys) in inventorySlots.VisualLayerKeys)
            {
                if (!_inventorySystem.TryGetSlotEntity(wearer, slot, out var item)) continue;
                if (!TryComp<ClothingComponent>(item, out var clothing)) continue;
                var slotFlags = clothing.InSlotFlag ?? SlotFlags.NONE;
                if ((slotFlags & SlotFlags.INNERCLOTHING) == 0) continue;
                foreach (var key in layerKeys)
                {
                    if (_sprite.LayerMapTryGet((wearer, sprite), key, out _, false))
                    {
                        _sprite.LayerSetVisible((wearer, sprite), key, false);
                        hidden.Add(key);
                    }
                }
            }
            return;
        }
        if (!_hiddenKeys.TryGetValue(wearer, out var previouslyHidden) || previouslyHidden.Count == 0) return;
        foreach (var key in previouslyHidden)
        { if (_sprite.LayerMapTryGet((wearer, sprite), key, out _, false)) { _sprite.LayerSetVisible((wearer, sprite), key, true); } }
        _hiddenKeys.Remove(wearer);
    }

    private bool HasOuterHiderEquipped(EntityUid wearer)
    {
        if (!TryComp<InventorySlotsComponent>(wearer, out var inventorySlots)) return false;
        foreach (var slot in inventorySlots.VisualLayerKeys.Keys)
        {
            if (!_inventorySystem.TryGetSlotEntity(wearer, slot, out var item)) continue;
            if (!HasComp<HideInnerClothingComponent>(item)) continue;
            if (!TryComp<ClothingComponent>(item, out var clothing)) continue;
            var flags = clothing.InSlotFlag ?? SlotFlags.NONE;
            if ((flags & SlotFlags.OUTERCLOTHING) != 0) return true;
        }
        return false;
    }

    private HashSet<string> GetOrCreateHiddenKeys(EntityUid wearer)
    {
        if (_hiddenKeys.TryGetValue(wearer, out var set)) return set;
        set = new HashSet<string>();
        _hiddenKeys.Add(wearer, set);
        return set;
    }
}
