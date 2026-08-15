// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server._Mono.FireControl;
using Content.Server.Disposal.Tube;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Lua.ShipMiningDrill;
using Content.Shared._Mono;
using Content.Shared.Audio;
using Content.Shared.Construction.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Disposal.Tube;
using Content.Shared.Examine;
using Content.Shared.Fluids.Components;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Lua.ShipMiningDrill;

public sealed class ShipMiningDrillSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DisposalTubeSystem _disposalTubes = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FireControlSystem _fireControl = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private readonly HashSet<EntityUid> _entities = [];
    private readonly HashSet<EntityUid> _minedThisTick = [];
    private readonly HashSet<EntityUid> _pickupBuffer = [];
    private readonly List<EntityUid> _flushBuffer = [];
    private List<Entity<MapGridComponent>> _grids = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShipMiningDrillComponent, FireControllableActivateEvent>(OnFireControlActivate);
        SubscribeLocalEvent<ShipMiningDrillComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ShipMiningDrillComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<ShipMiningDrillComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ShipMiningDrillComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShipMiningDrillComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drill, out var xform))
        {
            if (!drill.Enabled)
                continue;

            if (!CanRun(uid, xform))
                continue;

            if (_timing.CurTime < drill.NextMine)
                continue;

            drill.NextMine = _timing.CurTime + TimeSpan.FromSeconds(drill.MineInterval);
            Mine(uid, drill, xform);
            PickupAndFlushOre(uid, drill, xform);
        }
    }

    private void OnFireControlActivate(Entity<ShipMiningDrillComponent> ent, ref FireControllableActivateEvent args)
    {
        args.Handled = true;
        args.Success = TryToggle(ent, popup: true);
    }

    private void OnPowerChanged(Entity<ShipMiningDrillComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            SetEnabled(ent, false);
    }

    private void OnAnchorChanged(Entity<ShipMiningDrillComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            SetEnabled(ent, false);
    }

    private void OnShutdown(Entity<ShipMiningDrillComponent> ent, ref ComponentShutdown args)
    {
        SetEnabled(ent, false);
    }

    private void OnExamined(Entity<ShipMiningDrillComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Enabled
            ? "ship-mining-drill-examined-on"
            : "ship-mining-drill-examined-off"));

        args.PushMarkup(Loc.GetString(FindDisposalEntry(ent, Transform(ent)) != default
            ? "ship-mining-drill-examined-trunk"
            : "ship-mining-drill-examined-no-trunk"));
    }

    private bool TryToggle(Entity<ShipMiningDrillComponent> ent, bool popup)
    {
        return SetEnabled(ent, !ent.Comp.Enabled, popup);
    }

    private bool SetEnabled(Entity<ShipMiningDrillComponent> ent, bool enabled, bool popup = false)
    {
        if (enabled && !CanRun(ent, Transform(ent)))
            enabled = false;

        if (ent.Comp.Enabled == enabled)
            return enabled;

        ent.Comp.Enabled = enabled;
        if (enabled)
            ent.Comp.NextMine = _timing.CurTime;

        Dirty(ent);

        _ambient.SetAmbience(ent, enabled);
        _appearance.SetData(ent, ShipMiningDrillVisuals.Enabled, enabled);

        if (_pointLight.TryGetLight(ent, out var light))
            _pointLight.SetEnabled(ent, enabled, light);

        if (TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
            _power.SetLoad(receiver, enabled ? ent.Comp.ActivePowerLoad : ent.Comp.IdlePowerLoad);

        if (popup)
        {
            _popup.PopupEntity(
                Loc.GetString(enabled ? "ship-mining-drill-toggled-on" : "ship-mining-drill-toggled-off"),
                ent);
        }

        return enabled;
    }

    private bool CanRun(EntityUid uid, TransformComponent xform)
    {
        if (!xform.Anchored)
            return false;

        if (!_power.IsPowered(uid))
            return false;

        if (xform.GridUid is not { } grid)
            return false;

        return _fireControl.CanFireWeapons(grid);
    }

    private void Mine(EntityUid uid, ShipMiningDrillComponent drill, TransformComponent xform)
    {
        _minedThisTick.Clear();

        var worldPos = _xform.GetWorldPosition(xform);
        var worldRot = _xform.GetWorldRotation(xform);
        var ourGrid = xform.GridUid;
        var mapId = xform.MapID;

        foreach (var localOffset in drill.MiningOffsets)
        {
            var targetPos = worldPos + worldRot.RotateVec(localOffset);
            MineAt(uid, drill, ourGrid, mapId, targetPos);
        }
    }

    private void MineAt(
        EntityUid drillUid,
        ShipMiningDrillComponent drill,
        EntityUid? ourGrid,
        MapId mapId,
        Vector2 worldPos)
    {
        var box = Box2.CenteredAround(worldPos, new Vector2(0.9f, 0.9f));
        var blockedByStructure = false;

        _entities.Clear();
        _lookup.GetEntitiesIntersecting(mapId, box, _entities, LookupFlags.Static | LookupFlags.Dynamic);

        foreach (var target in _entities)
        {
            if (!_minedThisTick.Add(target))
                continue;

            var result = TryMineEntity(drillUid, drill, ourGrid, target);
            if (result == MineResult.None)
                _minedThisTick.Remove(target);
            else if (result == MineResult.Structure)
                blockedByStructure = true;
        }

        if (blockedByStructure)
            return;

        _grids.Clear();
        _mapManager.FindGridsIntersecting(mapId, box, ref _grids, approx: true, includeMap: false);

        foreach (var grid in _grids)
        {
            if (grid.Owner == ourGrid || HasComp<GridGodModeComponent>(grid.Owner))
                continue;

            var indices = _map.WorldToTile(grid.Owner, grid.Comp, worldPos);
            if (TileHasBlockingEntity(drillUid, grid.Owner, grid.Comp, indices))
                continue;

            var tile = _map.GetTileRef(grid.Owner, grid.Comp, indices);
            if (tile.Tile.IsEmpty)
                continue;

            _map.SetTile(grid.Owner, grid.Comp, indices, Tile.Empty);
        }
    }

    private MineResult TryMineEntity(EntityUid drillUid, ShipMiningDrillComponent drill, EntityUid? ourGrid, EntityUid target)
    {
        if (target == drillUid || TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target))
            return MineResult.None;

        var targetXform = Transform(target);
        if (targetXform.GridUid == ourGrid)
            return MineResult.None;

        if (targetXform.GridUid is { } targetGrid && HasComp<GridGodModeComponent>(targetGrid))
            return MineResult.None;

        if (HasComp<GodmodeComponent>(target) || HasComp<MapGridComponent>(target))
            return MineResult.None;

        if (HasComp<PuddleComponent>(target) ||
            HasComp<ItemComponent>(target) ||
            HasComp<DisposalEntryComponent>(target) ||
            HasComp<DisposalTubeComponent>(target))
            return MineResult.None;

        var isStructure = IsBlockingStructure(target, targetXform);

        if (isStructure && TryComp<GatherableComponent>(target, out var gatherable))
        {
            _gatherable.Gather(target, drillUid, gatherable);
            return MineResult.Structure;
        }

        if (drill.EntityDamage.Empty)
            return MineResult.None;

        if (!isStructure && !HasComp<MobStateComponent>(target))
            return MineResult.None;

        _damageable.TryChangeDamage(target, drill.EntityDamage, origin: drillUid);
        return isStructure ? MineResult.Structure : MineResult.Entity;
    }

    private bool IsBlockingStructure(EntityUid uid, TransformComponent xform)
    {
        if (!xform.Anchored)
            return false;

        if (HasComp<PuddleComponent>(uid) || HasComp<MobStateComponent>(uid) || HasComp<ItemComponent>(uid))
            return false;

        return HasComp<AnchorableComponent>(uid) || HasComp<GatherableComponent>(uid);
    }

    private bool TileHasBlockingEntity(EntityUid drillUid, EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        foreach (var ent in _map.GetAnchoredEntities(gridUid, grid, indices))
        {
            if (ent == drillUid || TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent))
                continue;

            if (HasComp<DisposalEntryComponent>(ent) || HasComp<DisposalTubeComponent>(ent))
                continue;

            if (IsBlockingStructure(ent, Transform(ent)))
                return true;
        }

        return false;
    }

    private void PickupAndFlushOre(EntityUid uid, ShipMiningDrillComponent drill, TransformComponent xform)
    {
        if (!_power.IsPowered(uid) || !xform.Anchored)
            return;

        var entry = FindDisposalEntry(uid, drill, xform);
        if (entry == default)
            return;

        var worldPos = _xform.GetWorldPosition(xform);
        var worldRot = _xform.GetWorldRotation(xform);
        var ourGrid = xform.GridUid;

        _pickupBuffer.Clear();
        foreach (var localOffset in drill.MiningOffsets)
        {
            var targetPos = worldPos + worldRot.RotateVec(localOffset);
            _lookup.GetEntitiesInRange(xform.MapID, targetPos, drill.PickupRange, _pickupBuffer, LookupFlags.Dynamic | LookupFlags.Sundries);
        }

        _flushBuffer.Clear();
        foreach (var item in _pickupBuffer)
        {
            if (_flushBuffer.Count >= drill.MaxPickupPerTick)
                break;

            if (!CanPickupOre(uid, drill, ourGrid, item))
                continue;

            _flushBuffer.Add(item);
        }

        if (_flushBuffer.Count == 0)
            return;

        _disposalTubes.TryInsert(entry, _flushBuffer);
    }

    private bool CanPickupOre(EntityUid drillUid, ShipMiningDrillComponent drill, EntityUid? ourGrid, EntityUid item)
    {
        if (item == drillUid || TerminatingOrDeleted(item) || EntityManager.IsQueuedForDeletion(item))
            return false;

        if (_containers.IsEntityInContainer(item))
            return false;

        if (!HasComp<ItemComponent>(item))
            return false;

        if (!_tags.HasAnyTag(item, drill.PickupTags))
            return false;

        var itemGrid = Transform(item).GridUid;
        if (itemGrid == ourGrid)
            return false;

        return itemGrid == null || !HasComp<GridGodModeComponent>(itemGrid.Value);
    }

    private EntityUid FindDisposalEntry(Entity<ShipMiningDrillComponent> ent, TransformComponent xform)
    {
        return FindDisposalEntry(ent.Owner, ent.Comp, xform);
    }

    private EntityUid FindDisposalEntry(EntityUid uid, ShipMiningDrillComponent drill, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return default;

        var worldPos = _xform.GetWorldPosition(xform);
        var worldRot = _xform.GetWorldRotation(xform);

        if (TryEntryAtOffset(gridUid, grid, worldPos, worldRot, drill.DisposalOffset, out var preferred))
            return preferred;

        foreach (var offset in drill.MountOffsets)
        {
            if (offset == drill.DisposalOffset)
                continue;

            if (TryEntryAtOffset(gridUid, grid, worldPos, worldRot, offset, out var entry))
                return entry;
        }

        return default;
    }

    private bool TryEntryAtOffset(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2 worldPos,
        Angle worldRot,
        Vector2 offset,
        out EntityUid entry)
    {
        var tile = _map.WorldToTile(gridUid, grid, worldPos + worldRot.RotateVec(offset));
        foreach (var ent in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (!HasComp<DisposalEntryComponent>(ent))
                continue;

            entry = ent;
            return true;
        }

        entry = default;
        return false;
    }

    private enum MineResult : byte
    {
        None,
        Structure,
        Entity,
    }
}
