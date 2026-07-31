// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server._Lua.Shipyard.Components;
using Content.Server.Mind;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Lua.Shipyard.Systems;

public sealed class ShuttleParkingSystem : EntitySystem
{
    private EntityUid? _parkingMap;
    public enum ShuttleParkingError : byte
    {
        Success,
        InvalidShuttle,
        AlreadyParked,
        ShuttleNotParked,
        NotDocked,
        OrganicsAboard,
        CryoPodAboard,
        InvalidDock,
        NoDockingPath,
        InvalidConsole,
    }

    public readonly record struct ShuttleParkingResult(ShuttleParkingError Error, string? OrganicName = null);
    private const float ParkingBuffer = 8f;
    private const float ParkingRingStep = 120f;
    private const float ParkingBaseSlotSize = 60f;
    [Dependency] private readonly DockingSystem _docking = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    public bool IsParked(EntityUid shuttleUid)
    { return HasComp<ParkedShuttleComponent>(shuttleUid); }
    public ShuttleParkingResult TryParkShuttle(EntityUid consoleUid, EntityUid shuttleUid)
    {
        if (!HasComp<ShuttleComponent>(shuttleUid) || !TryComp<MapGridComponent>(shuttleUid, out var shuttleGrid))
            return new ShuttleParkingResult(ShuttleParkingError.InvalidShuttle);
        if (HasComp<ParkedShuttleComponent>(shuttleUid))
            return new ShuttleParkingResult(ShuttleParkingError.AlreadyParked);
        if (!IsDockedToConsoleStation(consoleUid, shuttleUid))
            return new ShuttleParkingResult(ShuttleParkingError.NotDocked);
        var organic = FoundOrganics(shuttleUid);
        if (organic != null)
            return new ShuttleParkingResult(ShuttleParkingError.OrganicsAboard, organic);
        if (HasPlayerCryoPod(shuttleUid))
            return new ShuttleParkingResult(ShuttleParkingError.CryoPodAboard);
        var parkingMap = EnsureParkingMap();
        var spawnPosition = FindParkingPosition(shuttleUid, shuttleGrid, parkingMap);
        _docking.UndockDocks(shuttleUid);
        if (TryComp<PhysicsComponent>(shuttleUid, out var body))
        {
            _physics.SetLinearVelocity(shuttleUid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(shuttleUid, 0f, body: body);
        }
        _transform.SetCoordinates(shuttleUid, new EntityCoordinates(parkingMap.Owner, spawnPosition));
        _transform.SetWorldRotation(shuttleUid, Angle.Zero);
        EnsureComp<ParkedShuttleComponent>(shuttleUid);
        return new ShuttleParkingResult(ShuttleParkingError.Success);
    }

    public ShuttleParkingResult TryRecallShuttle(EntityUid consoleUid, EntityUid shuttleUid, EntityUid targetDockUid)
    {
        if (!HasComp<ParkedShuttleComponent>(shuttleUid))
            return new ShuttleParkingResult(ShuttleParkingError.ShuttleNotParked);
        if (!TryComp<ShuttleComponent>(shuttleUid, out var shuttle))
            return new ShuttleParkingResult(ShuttleParkingError.InvalidShuttle);
        if (!TryComp<DockingComponent>(targetDockUid, out _) || !TryComp<TransformComponent>(targetDockUid, out var dockXform) || dockXform.GridUid is not { Valid: true } targetGrid)
        { return new ShuttleParkingResult(ShuttleParkingError.InvalidDock); }
        if (_station.GetOwningStation(consoleUid) is not { Valid: true } consoleStation || _station.GetOwningStation(targetDockUid) != consoleStation)
        { return new ShuttleParkingResult(ShuttleParkingError.InvalidConsole); }
        var config = _docking.GetDockingConfigForGridDock(shuttleUid, targetGrid, targetDockUid);
        if (config == null) return new ShuttleParkingResult(ShuttleParkingError.NoDockingPath);
        _docking.UndockDocks(shuttleUid);
        _shuttle.FTLDock((shuttleUid, Transform(shuttleUid)), config);
        RemComp<ParkedShuttleComponent>(shuttleUid);
        return new ShuttleParkingResult(ShuttleParkingError.Success);
    }

    private Entity<ParkingMapComponent> EnsureParkingMap()
    {
        if (_parkingMap is { Valid: true } parkingUid && TryComp<ParkingMapComponent>(parkingUid, out var parkingComp))
        { return (parkingUid, parkingComp); }
        var mapUid = _map.CreateMap(out var mapId);
        var parking = EnsureComp<ParkingMapComponent>(mapUid);
        _metaData.SetEntityName(mapUid, "Parking map");
        _map.SetPaused(mapId, true);
        _parkingMap = mapUid;
        return (mapUid, parking);
    }

    private Vector2 FindParkingPosition(EntityUid shuttleUid, MapGridComponent shuttleGrid, Entity<ParkingMapComponent> parkingMap)
    {
        var mapId = Transform(parkingMap.Owner).MapID;
        var shuttleAabb = shuttleGrid.LocalAABB;
        var intersecting = new List<Entity<MapGridComponent>>();

        var ring = parkingMap.Comp.CurrentRing;
        var slot = parkingMap.Comp.CurrentSlotInRing;

        while (true)
        {
            var totalSlots = GetSlotsForRing(ring);
            while (slot < totalSlots)
            {
                var center = GetRingSlotPosition(ring, slot, totalSlots);
                var spawnPosition = center - shuttleAabb.Center;
                var aabb = shuttleAabb.Translated(spawnPosition).Enlarged(ParkingBuffer / 2f);
                intersecting.Clear();
                _mapManager.FindGridsIntersecting(mapId, aabb, ref intersecting);
                intersecting.RemoveAll(ent => ent.Owner == shuttleUid);
                if (intersecting.Count == 0)
                {
                    var nextSlot = slot + 1;
                    if (nextSlot >= totalSlots)
                    {
                        parkingMap.Comp.CurrentRing = ring + 1;
                        parkingMap.Comp.CurrentSlotInRing = 0;
                    }
                    else
                    {
                        parkingMap.Comp.CurrentRing = ring;
                        parkingMap.Comp.CurrentSlotInRing = nextSlot;
                    }
                    return spawnPosition;
                }
                slot++;
            }
            ring++;
            slot = 0;
        }
    }
    private static int GetSlotsForRing(int ring)
    {
        if (ring == 0) return 1;
        var radius = ring * ParkingRingStep;
        return Math.Max(4, (int)(MathF.PI * 2f * radius / ParkingBaseSlotSize));
    }

    private static Vector2 GetRingSlotPosition(int ring, int slot, int totalSlots)
    {
        if (ring == 0) return Vector2.Zero;
        var radius = ring * ParkingRingStep;
        var angle = slot * (MathF.PI * 2f / totalSlots);
        return new Vector2(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
    }

    private bool IsDockedToConsoleStation(EntityUid consoleUid, EntityUid shuttleUid)
    {
        if (_station.GetOwningStation(consoleUid) is not { Valid: true } stationUid || !TryComp<StationDataComponent>(stationUid, out var stationData))
        { return false; }
        var stationGrid = _station.GetLargestGrid(stationData);
        if (stationGrid == null) return false;
        var gridDocks = _docking.GetDocks(stationGrid.Value);
        var shuttleDocks = _docking.GetDocks(shuttleUid);
        foreach (var shuttleDock in shuttleDocks)
        { foreach (var gridDock in gridDocks) { if (shuttleDock.Comp.DockedWith == gridDock.Owner) return true; } }
        return false;
    }

    private string? FoundOrganics(EntityUid uid)
    {
        if (!TryComp<TransformComponent>(uid, out var xform)) return null;
        var childEnumerator = xform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (HasComp<GhostComponent>(child)) continue;
            if (_mind.TryGetMind(child, out _, out var mindComp) && (mindComp.UserId != null || !_mind.IsCharacterDeadPhysically(mindComp)))
            { return Name(child); }
            var nested = FoundOrganics(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private bool HasPlayerCryoPod(EntityUid uid)
    {
        if (!TryComp<TransformComponent>(uid, out var xform)) return false;
        var childEnumerator = xform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (TryComp<MetaDataComponent>(child, out var meta) && meta.EntityPrototype?.ID == "MachineCryoSleepPodPlayer")
            { return true; }
            if (HasPlayerCryoPod(child)) return true;
        }
        return false;
    }
}
