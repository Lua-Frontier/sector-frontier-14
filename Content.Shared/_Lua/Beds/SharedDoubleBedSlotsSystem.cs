// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Content.Shared._Lua.Beds.Components;
using Content.Shared.Buckle.Components;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._Lua.Beds;

public sealed class SharedDoubleBedSlotsSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoubleStrapComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DoubleStrapComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DoubleStrapComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<DoubleStrapComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<DoubleStrapComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnStartup(Entity<DoubleStrapComponent> ent, ref ComponentStartup args)
    {
        if (_netManager.IsClient) return;
        EnsureCapacity(ent);
    }

    private void OnShutdown(Entity<DoubleStrapComponent> ent, ref ComponentShutdown args)
    {
        if (_netManager.IsClient) return;
        ent.Comp.Strap.Clear();
    }

    private void OnAfterState(Entity<DoubleStrapComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_netManager.IsClient) return;
        EnsureCapacity(ent);
    }

    private void OnStrapped(Entity<DoubleStrapComponent> ent, ref StrappedEvent args)
    {
        if (_netManager.IsClient || !IsRelevantStrap(ent, args.Strap)) return;
        EnsureCapacity(ent);
        var buckleUid = args.Buckle.Owner;
        if (TryFindSlot(ent, buckleUid, out _)) return;
        if (!TryGetFreeSlot(ent, out var slotIndex)) return;
        ent.Comp.Strap[slotIndex] = buckleUid;
        MoveToSlot(ent, buckleUid, slotIndex);
    }

    private void OnUnstrapped(Entity<DoubleStrapComponent> ent, ref UnstrappedEvent args)
    {
        if (_netManager.IsClient || !IsRelevantStrap(ent, args.Strap)) return;
        if (!TryFindSlot(ent, args.Buckle.Owner, out var slotIndex)) return;
        ent.Comp.Strap[slotIndex] = null;
    }

    private void MoveToSlot(Entity<DoubleStrapComponent> ent, EntityUid buckleUid, int slotIndex)
    {
        if (_netManager.IsClient) return;
        if (slotIndex < 0 || slotIndex >= ent.Comp.Slots.Count) return;
        var offset = ent.Comp.Slots[slotIndex];
        var strapOwner = ent.Comp.StrapOverride ?? ent.Owner;
        var buckleXform = Transform(buckleUid);
        var coords = new EntityCoordinates(strapOwner, offset);
        _transform.SetCoordinates(buckleUid, buckleXform, coords, rotation: Angle.Zero);
    }

    private bool TryGetFreeSlot(Entity<DoubleStrapComponent> ent, out int slotIndex)
    {
        for (var i = 0; i < ent.Comp.Strap.Count; i++)
        {
            var straps = ent.Comp.Strap[i];
            if (straps == null || straps == EntityUid.Invalid)
            {
                slotIndex = i;
                return true;
            }
        }
        slotIndex = -1;
        return false;
    }

    private bool TryFindSlot(Entity<DoubleStrapComponent> ent, EntityUid buckleUid, out int slotIndex)
    {
        for (var i = 0; i < ent.Comp.Strap.Count; i++)
        {
            if (ent.Comp.Strap[i] == buckleUid)
            {
                slotIndex = i;
                return true;
            }
        }
        slotIndex = -1;
        return false;
    }

    private bool IsRelevantStrap(Entity<DoubleStrapComponent> ent, Entity<StrapComponent> strap)
    {
        var strapOwner = ent.Comp.StrapOverride ?? ent.Owner;
        return strap.Owner == strapOwner;
    }

    private void EnsureCapacity(Entity<DoubleStrapComponent> ent)
    {
        if (_netManager.IsClient) return;
        var strapOwner = ent.Comp.StrapOverride ?? ent.Owner;
        if (!TryComp<StrapComponent>(strapOwner, out var strap)) return;
        var slotCount = ent.Comp.Slots.Count;
        while (ent.Comp.Strap.Count < slotCount) ent.Comp.Strap.Add(null);
        if (ent.Comp.Strap.Count > slotCount) ent.Comp.Strap.RemoveRange(slotCount, ent.Comp.Strap.Count - slotCount);
        for (var i = 0; i < ent.Comp.Strap.Count; i++)
        {
            var straps = ent.Comp.Strap[i];
            if (straps == null || TerminatingOrDeleted(straps.Value))
            {
                ent.Comp.Strap[i] = null;
                continue;
            }
            if (!TryComp(straps.Value, out BuckleComponent? buckle) || buckle.BuckledTo != strapOwner) ent.Comp.Strap[i] = null;
        }
        foreach (var straps in strap.BuckledEntities)
        {
            if (TryFindSlot(ent, straps, out _)) continue;
            if (!TryGetFreeSlot(ent, out var slotIndex)) continue;
            ent.Comp.Strap[slotIndex] = straps;
            MoveToSlot(ent, straps, slotIndex);
        }
    }
}

