// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using System.Numerics;
using Content.Shared._Lua.Beds.Components;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._Lua.Beds;

public sealed class DoubleStrapSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, Vector2> _originalOffsets = new();
    private readonly HashSet<EntityUid> _touched = new();
    private readonly Dictionary<EntityUid, Dictionary<EntityUid, int>> _strapAssignments = new();
    private readonly Dictionary<EntityUid, List<EntityUid>> _strapSnapshots = new();
    private readonly Dictionary<EntityUid, List<bool>> _strapTakenCache = new();
    private readonly HashSet<EntityUid> _visitedStraps = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _touched.Clear();
        _visitedStraps.Clear();
        var camRot = _eye.CurrentEye.Rotation;
        var camDir = camRot.GetCardinalDir();
        using var enumerator = EntityQueryEnumerator<DoubleStrapComponent, StrapComponent>();
        while (enumerator.MoveNext(out var strapUid, out var slotsComp, out var strapComp))
        {
            _visitedStraps.Add(strapUid);
            if (!strapComp.Enabled) continue;
            var cameraSlots = GetCameraSlots(slotsComp, camDir);
            if (cameraSlots.Count == 0 || slotsComp.Slots.Count == 0) continue;
            var buckledCount = strapComp.BuckledEntities.Count;
            if (buckledCount == 0) continue;
            if (!_strapSnapshots.TryGetValue(strapUid, out var snapshot)) snapshot = _strapSnapshots[strapUid] = new List<EntityUid>(buckledCount);
            snapshot.Clear();
            foreach (var buckled in strapComp.BuckledEntities) snapshot.Add(buckled);
            if (snapshot.Count == 0) continue;
            if (!_strapAssignments.TryGetValue(strapUid, out var assigned)) assigned = _strapAssignments[strapUid] = new Dictionary<EntityUid, int>();
            var removalBuffer = assigned.Count > 0 ? new List<EntityUid>(assigned.Count) : null;
            foreach (var prev in assigned.Keys)
            {
                var stillHere = false;
                for (var i = 0; i < snapshot.Count; i++)
                {
                    if (snapshot[i] == prev)
                    {
                        stillHere = true;
                        break;
                    }
                }
                if (!stillHere) removalBuffer?.Add(prev);
            }
            if (removalBuffer != null)
            {
                for (var i = 0; i < removalBuffer.Count; i++) assigned.Remove(removalBuffer[i]);
                removalBuffer.Clear();
            }

            var maxSlots = Math.Min(slotsComp.Slots.Count, cameraSlots.Count);
            if (maxSlots == 0) continue;
            if (!_strapTakenCache.TryGetValue(strapUid, out var taken)) taken = _strapTakenCache[strapUid] = new List<bool>(maxSlots);
            if (taken.Count < maxSlots)
            {
                var needed = maxSlots - taken.Count;
                for (var i = 0; i < needed; i++) taken.Add(false);
            }
            for (var i = 0; i < maxSlots; i++) taken[i] = false;
            foreach (var index in assigned.Values)
            { if ((uint)index < (uint)maxSlots) taken[index] = true; }
            for (var i = 0; i < snapshot.Count; i++)
            {
                var straps = snapshot[i];
                if (assigned.ContainsKey(straps)) continue;
                var chosen = -1;
                for (var slot = 0; slot < maxSlots; slot++)
                {
                    if (taken[slot]) continue;
                    taken[slot] = true;
                    chosen = slot;
                    break;
                }
                if (chosen != -1) assigned[straps] = chosen;
            }

            foreach (var (straps, index) in assigned)
            {
                if ((uint)index >= (uint)slotsComp.Slots.Count) continue;
                if (!TryComp(straps, out SpriteComponent? sprite)) continue;
                _touched.Add(straps);
                if (!_originalOffsets.TryGetValue(straps, out var original))
                {
                    original = sprite.Offset;
                    _originalOffsets[straps] = original;
                }
                var baseSlot = slotsComp.Slots[index];
                var targetSlot = index < cameraSlots.Count ? cameraSlots[index] : baseSlot;
                var desired = original + (targetSlot - baseSlot);
                if (!sprite.Offset.EqualsApprox(desired)) _sprite.SetOffset((straps, sprite), desired);
            }
        }
        if (_originalOffsets.Count == 0) return;
        foreach (var (uid, original) in _originalOffsets.ToArray())
        {
            if (_touched.Contains(uid)) continue;
            if (TryComp(uid, out SpriteComponent? sprite)) _sprite.SetOffset((uid, sprite), original);
            _originalOffsets.Remove(uid);
        }
        CleanupStraps();
    }

    private static IReadOnlyList<Vector2> GetCameraSlots(DoubleStrapComponent component, Direction cameraDir)
    {
        var list = cameraDir switch
        {
            Direction.North => component.SlotsCameraNorth,
            Direction.East => component.SlotsCameraEast,
            Direction.South => component.SlotsCameraSouth,
            Direction.West => component.SlotsCameraWest,
            _ => null
        };
        return list ?? component.Slots;
    }

    private void CleanupStraps()
    {
        if (_strapAssignments.Count == _visitedStraps.Count) return;
        var toRemove = new List<EntityUid>();
        foreach (var key in _strapAssignments.Keys)
        {
            if (_visitedStraps.Contains(key)) continue;
            toRemove.Add(key);
        }
        foreach (var key in toRemove)
        {
            _strapAssignments.Remove(key);
            _strapSnapshots.Remove(key);
            _strapTakenCache.Remove(key);
        }
    }
}
