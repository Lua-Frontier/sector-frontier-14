// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Starmap.Components;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class CoordinatesDiskMergerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CoordinatesDiskMergerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CoordinatesDiskMergerComponent, EntRemovedFromContainerMessage>(OnRemoved);
        Subs.BuiEvents<CoordinatesDiskMergerComponent>(DiskMergerUiKey.Key, subs =>
        { subs.Event<BoundUIOpenedEvent>(OnUiOpened); });
    }

    private void OnInserted(Entity<CoordinatesDiskMergerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        TryStartMerge(ent);
        PushState(ent.Owner, ent.Comp);
    }

    private void OnRemoved(Entity<CoordinatesDiskMergerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        ent.Comp.IsMerging = false;
        ent.Comp.MergeStartedAt = TimeSpan.Zero;
        PushState(ent.Owner, ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var q = AllEntityQuery<CoordinatesDiskMergerComponent>();
        while (q.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsMerging) continue;
            var duration = Math.Max(0.1f, comp.MergeDurationSeconds);
            var elapsed = (now - comp.MergeStartedAt).TotalSeconds;
            if (elapsed >= duration)
            {
                MergeNow((uid, comp));
                comp.IsMerging = false;
                comp.MergeStartedAt = TimeSpan.Zero;
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg"), uid);
                PushState(uid, comp);
            }
            else
            { PushState(uid, comp); }
        }
    }

    private void TryStartMerge(Entity<CoordinatesDiskMergerComponent> ent)
    {
        if (!TryGetDisk(ent.Owner, ent.Comp.SlotA, out _, out _)) return;
        if (!TryGetDisk(ent.Owner, ent.Comp.SlotB, out _, out _)) return;
        if (ent.Comp.IsMerging) return;
        ent.Comp.IsMerging = true;
        ent.Comp.MergeStartedAt = _timing.CurTime;
    }

    private void MergeNow(Entity<CoordinatesDiskMergerComponent> ent)
    {
        if (!TryGetDisk(ent.Owner, ent.Comp.SlotA, out var a, out var aComp)) return;
        if (!TryGetDisk(ent.Owner, ent.Comp.SlotB, out var b, out var bComp)) return;
        var setIds = new HashSet<string>(aComp.AllowedSectorIds);
        foreach (var s in bComp.AllowedSectorIds) setIds.Add(s);
        aComp.AllowedSectorIds = setIds.ToList();
        bComp.AllowedSectorIds = setIds.ToList();
        var setNames = new HashSet<string>(aComp.AllowedSectors);
        foreach (var s in bComp.AllowedSectors) setNames.Add(s);
        aComp.AllowedSectors = setNames.ToList();
        bComp.AllowedSectors = setNames.ToList();
        aComp.AllowFtlToCentCom = aComp.AllowFtlToCentCom || bComp.AllowFtlToCentCom;
        bComp.AllowFtlToCentCom = aComp.AllowFtlToCentCom;
    }

    private void OnUiOpened(Entity<CoordinatesDiskMergerComponent> ent, ref BoundUIOpenedEvent args)
    { PushState(ent.Owner, ent.Comp); }

    private void PushState(EntityUid uid, CoordinatesDiskMergerComponent comp)
    {
        var hasA = TryGetDisk(uid, comp.SlotA, out var a, out var aComp);
        var hasB = TryGetDisk(uid, comp.SlotB, out var b, out var bComp);
        var aName = hasA && TryComp<MetaDataComponent>(a, out var aMeta) ? aMeta.EntityName : string.Empty;
        var bName = hasB && TryComp<MetaDataComponent>(b, out var bMeta) ? bMeta.EntityName : string.Empty;
        var now = _timing.CurTime;
        var progress = 0f;
        if (comp.IsMerging && comp.MergeDurationSeconds > 0)
        {
            progress = (float) ((now - comp.MergeStartedAt).TotalSeconds / comp.MergeDurationSeconds);
            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;
        }
        string[] GetDisplayList(StarMapCoordinatesDiskComponent c)
        {
            if (c.AllowedSectorIds != null && c.AllowedSectorIds.Count > 0) return c.AllowedSectorIds.ToArray();
            if (c.AllowedSectors != null && c.AllowedSectors.Count > 0) return c.AllowedSectors.ToArray();
            return Array.Empty<string>();
        }
        var state = new DiskMergerBuiState(comp.IsMerging, progress, hasA, hasB, aName, bName, hasA ? GetDisplayList(aComp) : Array.Empty<string>(), hasB ? GetDisplayList(bComp) : Array.Empty<string>()); _ui.SetUiState(uid, DiskMergerUiKey.Key, state);
    }

    private bool TryGetDisk(EntityUid owner, string slot, out EntityUid disk, out StarMapCoordinatesDiskComponent comp)
    {
        disk = default;
        comp = default!;
        if (!TryComp<ContainerManagerComponent>(owner, out var contMan)) return false;
        if (!contMan.TryGetContainer(slot, out var container)) return false;
        if (container.ContainedEntities.Count == 0) return false;
        var ent = container.ContainedEntities[0];
        if (!TryComp(ent, out StarMapCoordinatesDiskComponent? compNullable)) return false;
        comp = compNullable!;
        disk = ent;
        return true;
    }
}


