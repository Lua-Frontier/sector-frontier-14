using Content.Server._Lua.Shipyard.Components;
using Content.Server._Lua.Stargate.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Lua.Stargate;
using Content.Shared._Lua.Stargate.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StarGateShuttleLandingSystem : EntitySystem
{
    private const float PlanetShuttleMassLimit = 230f;
    private const float BeaconBlockRadius = 6f;
    private const float ConsoleBlockRadius = 8f;
    private const float GateBlockRadius = 8f;
    private static readonly TimeSpan AutoReturnDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan RecallDelay = TimeSpan.FromMinutes(8);
    private static readonly TimeSpan RecallUiUpdateInterval = TimeSpan.FromSeconds(1);
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedIdCardSystem _idCards = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;
    private List<Entity<MapGridComponent>> _gridIntersectionBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _actorQuery = GetEntityQuery<ActorComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        SubscribeLocalEvent<StarGateLandingBeaconComponent, ComponentInit>(OnBeaconInit);
        SubscribeLocalEvent<StarGateLandingBeaconComponent, StarGateLandingBeaconSummonMessage>(OnBeaconSummon);
        SubscribeLocalEvent<StarGateLandingBeaconComponent, StarGateLandingBeaconFlyToMessage>(OnBeaconFlyTo);
        SubscribeLocalEvent<StarGateLandingBeaconComponent, StarGateLandingBeaconRecallMessage>(OnBeaconRecall);
        SubscribeLocalEvent<StarGateLandingBeaconComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<StarGateLandingBeaconComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<StarGateLandingBeaconComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.BoundShuttle is not { } shuttleUid || Deleted(shuttleUid)) continue;
            if (_xformQuery.TryGetComponent(shuttleUid, out var shuttleXform) && shuttleXform.MapUid is { } shuttleMapUid && HasComp<StargateDestinationComponent>(shuttleMapUid))
            {
                var hasValidPlayers = MapHasValidPlayers(shuttleMapUid);
                if (hasValidPlayers)
                {
                    if (comp.AutoReturnAt != null)
                    { comp.AutoReturnAt = null; }
                }
                else
                {
                    comp.AutoReturnAt ??= now + AutoReturnDelay;
                    if (now >= comp.AutoReturnAt.Value && comp.ReturnMapUid != null && TryComp<ShuttleComponent>(shuttleUid, out var shuttleComp) && _shuttle.CanFTL(shuttleUid, out _) && !HasComp<FTLComponent>(shuttleUid))
                    {
                        var destination = new EntityCoordinates(comp.ReturnMapUid.Value, comp.ReturnWorldPosition);
                        _shuttle.FTLToCoordinates(shuttleUid, shuttleComp, destination, comp.ReturnAngle);
                        comp.AutoReturnAt = null;
                        comp.RecallAt = null;
                        comp.RecallFtlStarted = false;
                        UpdateUiState((uid, comp));
                        continue;
                    }
                }
            }
            else if (comp.AutoReturnAt != null)
            { comp.AutoReturnAt = null; }
            if (comp.RecallAt == null) continue;
            if (!comp.RecallFtlStarted)
            {
                var remaining = comp.RecallAt.Value - now;
                var launchThreshold = TimeSpan.FromSeconds(_shuttle.DefaultStartupTime) + TimeSpan.FromSeconds(0.5f);
                if (remaining <= launchThreshold && TryComp<ShuttleComponent>(shuttleUid, out var shuttleComp) && comp.ReturnMapUid != null && _shuttle.CanFTL(shuttleUid, out _))
                {
                    var startupTime = (float) remaining.TotalSeconds;
                    if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime)) startupTime = MathF.Max(0f, startupTime - 0.5f);
                    startupTime = MathF.Min(startupTime, _shuttle.DefaultStartupTime);
                    var destination = new EntityCoordinates(comp.ReturnMapUid.Value, comp.ReturnWorldPosition);
                    _shuttle.FTLToCoordinates(shuttleUid, shuttleComp, destination, comp.ReturnAngle, startupTime, _shuttle.DefaultTravelTime);
                    comp.RecallFtlStarted = true;
                    UpdateUiState((uid, comp));
                    continue;
                }
            }
            if (now >= comp.RecallAt.Value)
            {
                comp.RecallAt = null;
                comp.RecallFtlStarted = false;
                UpdateUiState((uid, comp));
                continue;
            }
            if (comp.NextRecallUiUpdate <= now)
            {
                comp.NextRecallUiUpdate = now + RecallUiUpdateInterval;
                UpdateUiState((uid, comp));
            }
        }
    }

    private void OnBeaconInit(Entity<StarGateLandingBeaconComponent> ent, ref ComponentInit args)
    { _itemSlots.AddItemSlot(ent, StarGateLandingBeaconComponent.DeedSlotId, ent.Comp.DeedSlot); }
    private void OnSlotChanged(Entity<StarGateLandingBeaconComponent> ent, ref EntInsertedIntoContainerMessage args)
    { UpdateUiState(ent); }
    private void OnSlotChanged(Entity<StarGateLandingBeaconComponent> ent, ref EntRemovedFromContainerMessage args)
    { UpdateUiState(ent); }

    private void UpdateUiState(Entity<StarGateLandingBeaconComponent> ent)
    {
        string? shuttleName = null;
        NetEntity? shuttleNetEntity = null;
        if (_itemSlots.TryGetSlot(ent, StarGateLandingBeaconComponent.DeedSlotId, out var deedSlot) && deedSlot.Item is { } slotItem && _idCards.TryGetIdCard(slotItem, out var idCard2) && TryComp<ShuttleDeedComponent>(idCard2, out var deed2) && deed2.ShuttleUid is { Valid: true } sUid && TryComp<MetaDataComponent>(sUid, out var meta))
        {
            shuttleName = meta.EntityName;
            shuttleNetEntity = GetNetEntity(sUid);
        }
        var beaconPos = _xformQuery.TryGetComponent(ent, out var xform) ? _transform.ToMapCoordinates(xform.Coordinates) : MapCoordinates.Nullspace;
        var recallRemaining = ent.Comp.RecallAt is { } recallAt ? recallAt - _timing.CurTime : TimeSpan.Zero;
        if (recallRemaining < TimeSpan.Zero) recallRemaining = TimeSpan.Zero;
        _ui.SetUiState(ent.Owner, StarGateLandingBeaconUiKey.Key,
            new StarGateLandingBeaconBoundUserInterfaceState(shuttleName, ent.Comp.BoundShuttle.HasValue, beaconPos, shuttleNetEntity, shuttleName != null && ent.Comp.BoundShuttle.HasValue && ent.Comp.ReturnMapUid != null, ent.Comp.RecallAt != null, recallRemaining));
    }

    private void OnBeaconSummon(Entity<StarGateLandingBeaconComponent> ent, ref StarGateLandingBeaconSummonMessage args)
    {
        var user = args.Actor;
        if (!_xformQuery.TryGetComponent(ent, out var beaconXform) || beaconXform.MapUid == null) return;
        if (!IsBeaconOnStargatePlanet(beaconXform))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-not-stargate-world"), ent, user);
            return;
        }
        if (!_itemSlots.TryGetSlot(ent, StarGateLandingBeaconComponent.DeedSlotId, out var deedSlot) || deedSlot.Item is not { } insertedItem)
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-no-id"), ent, user);
            return;
        }
        if (!_idCards.TryGetIdCard(insertedItem, out var idCard))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-no-bound-shuttle"), ent, user);
            return;
        }
        if (!TryComp<ShuttleDeedComponent>(idCard, out var deed) || deed.ShuttleUid is not { Valid: true } shuttleUid || !Exists(shuttleUid))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-no-bound-shuttle"), ent, user);
            return;
        }
        if (!_gridQuery.HasComp(shuttleUid) || !TryComp<ShuttleComponent>(shuttleUid, out var shuttleComp))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-no-bound-shuttle"), ent, user);
            return;
        }
        if (HasComp<ParkedShuttleComponent>(shuttleUid))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-shuttle-parked"), ent, user);
            return;
        }
        if (_physicsQuery.TryGetComponent(shuttleUid, out var shuttlePhysics) && shuttlePhysics.Mass > PlanetShuttleMassLimit)
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-too-heavy", ("mass", PlanetShuttleMassLimit)), ent, user);
            return;
        }
        if (!_shuttle.CanFTL(shuttleUid, out var reason))
        {
            _popup.PopupEntity(reason ?? Loc.GetString("shuttle-console-noftl"), ent, user);
            return;
        }
        var beaconPos = _transform.ToMapCoordinates(beaconXform.Coordinates).Position;
        var beaconAngle = _transform.GetWorldRotation(ent);
        var offset = beaconAngle.RotateVec(new Vector2(ent.Comp.EdgeOffsetTiles, 0f));
        var desiredCenterWorld = beaconPos + offset;
        var targetWorld = GetLandingWorldTarget(shuttleUid, desiredCenterWorld, beaconAngle);
        var targetCoords = new MapCoordinates(targetWorld, _transform.GetMapId(beaconXform.Coordinates));
        if (!IsLandingTargetAllowed(shuttleUid, targetCoords, beaconAngle, beaconXform, out var blockedLocKey))
        {
            _popup.PopupEntity(Loc.GetString(blockedLocKey), ent, user);
            return;
        }
        var mapGridXform = _xformQuery.GetComponent(beaconXform.MapUid.Value);
        var localTargetPos = Vector2.Transform(targetWorld, mapGridXform.InvLocalMatrix);
        var target = new EntityCoordinates(beaconXform.MapUid.Value, localTargetPos);
        StoreReturnPosition((ent, ent.Comp), shuttleUid, _transform.GetMapId(beaconXform.Coordinates));
        ent.Comp.BoundShuttle = shuttleUid;
        ent.Comp.AutoReturnAt = null;
        ent.Comp.RecallAt = null;
        ent.Comp.RecallFtlStarted = false;
        _shuttle.FTLToCoordinates(shuttleUid, shuttleComp, target, beaconAngle);
        UpdateUiState(ent);
        _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-summon-ok"), ent, user);
    }

    private void OnBeaconFlyTo(Entity<StarGateLandingBeaconComponent> ent, ref StarGateLandingBeaconFlyToMessage args)
    {
        var user = args.Actor;
        if (!_xformQuery.TryGetComponent(ent, out var beaconXform) || beaconXform.MapUid == null) return;
        if (!IsBeaconOnStargatePlanet(beaconXform))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-not-stargate-world"), ent, user);
            return;
        }
        if (!_itemSlots.TryGetSlot(ent, StarGateLandingBeaconComponent.DeedSlotId, out var deedSlot) || deedSlot.Item is not { } insertedItem)
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-no-id"), ent, user);
            return;
        }
        if (!_idCards.TryGetIdCard(insertedItem, out var idCard))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-no-bound-shuttle"), ent, user);
            return;
        }
        if (!TryComp<ShuttleDeedComponent>(idCard, out var deed) || deed.ShuttleUid is not { Valid: true } shuttleUid || !Exists(shuttleUid))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-no-bound-shuttle"), ent, user);
            return;
        }
        if (!_gridQuery.HasComp(shuttleUid) || !TryComp<ShuttleComponent>(shuttleUid, out var shuttleComp))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-no-bound-shuttle"), ent, user);
            return;
        }
        if (HasComp<ParkedShuttleComponent>(shuttleUid))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-shuttle-parked"), ent, user);
            return;
        }
        if (_physicsQuery.TryGetComponent(shuttleUid, out var shuttlePhysics) && shuttlePhysics.Mass > PlanetShuttleMassLimit)
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-too-heavy", ("mass", PlanetShuttleMassLimit)), ent, user);
            return;
        }
        if (!_shuttle.CanFTL(shuttleUid, out var reason))
        {
            _popup.PopupEntity(reason ?? Loc.GetString("shuttle-console-noftl"), ent, user);
            return;
        }
        var targetCoords = args.Coordinates;
        if (targetCoords.MapId != _transform.GetMapId(beaconXform.Coordinates))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-wrong-map"), ent, user);
            return;
        }
        var targetWorld = GetLandingWorldTarget(shuttleUid, targetCoords.Position, args.Angle);
        var resolvedTargetCoords = new MapCoordinates(targetWorld, targetCoords.MapId);
        if (!IsLandingTargetAllowed(shuttleUid, resolvedTargetCoords, args.Angle, beaconXform, out var blockedLocKey))
        {
            _popup.PopupEntity(Loc.GetString(blockedLocKey), ent, user);
            return;
        }
        var mapGridXform = _xformQuery.GetComponent(beaconXform.MapUid.Value);
        var localTargetPos = Vector2.Transform(targetWorld, mapGridXform.InvLocalMatrix);
        var target = new EntityCoordinates(beaconXform.MapUid.Value, localTargetPos);
        StoreReturnPosition((ent, ent.Comp), shuttleUid, _transform.GetMapId(beaconXform.Coordinates));
        ent.Comp.BoundShuttle = shuttleUid;
        ent.Comp.AutoReturnAt = null;
        ent.Comp.RecallAt = null;
        ent.Comp.RecallFtlStarted = false;
        _shuttle.FTLToCoordinates(shuttleUid, shuttleComp, target, args.Angle);
        UpdateUiState(ent);
        _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-summon-ok"), ent, user);
    }

    private Vector2 GetLandingWorldTarget(EntityUid shuttleUid, Vector2 desiredCenterWorld, Angle angle)
    {
        var targetWorld = desiredCenterWorld;
        if (_physicsQuery.TryGetComponent(shuttleUid, out var physics)) targetWorld -= angle.RotateVec(physics.LocalCenter);
        return targetWorld;
    }

    private void OnBeaconRecall(Entity<StarGateLandingBeaconComponent> ent, ref StarGateLandingBeaconRecallMessage args)
    {
        var user = args.Actor;
        if (!_xformQuery.TryGetComponent(ent, out var beaconXform) || !IsBeaconOnStargatePlanet(beaconXform))
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-not-stargate-world"), ent, user);
            return;
        }
        if (ent.Comp.BoundShuttle is not { } shuttleUid || !Exists(shuttleUid) || ent.Comp.ReturnMapUid == null)
        {
            _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-recall-unavailable"), ent, user);
            return;
        }
        if (ent.Comp.RecallAt != null) return;
        ent.Comp.RecallAt = _timing.CurTime + RecallDelay;
        ent.Comp.RecallFtlStarted = false;
        ent.Comp.NextRecallUiUpdate = _timing.CurTime + RecallUiUpdateInterval;
        UpdateUiState(ent);
        _popup.PopupEntity(Loc.GetString("stargate-shuttle-beacon-recall-started", ("minutes", (int) RecallDelay.TotalMinutes)), ent, user);
    }

    private void StoreReturnPosition(Entity<StarGateLandingBeaconComponent> beacon, EntityUid shuttleUid, MapId targetMapId)
    {
        if (!_xformQuery.TryGetComponent(shuttleUid, out var shuttleXform) || shuttleXform.MapUid == null) return;
        if (HasComp<StargateDestinationComponent>(shuttleXform.MapUid.Value)) return;
        if (_transform.GetMapId(shuttleXform.Coordinates) == targetMapId && beacon.Comp.ReturnMapUid != null) return;
        beacon.Comp.ReturnMapUid = shuttleXform.MapUid.Value;
        beacon.Comp.ReturnWorldPosition = _transform.GetWorldPosition(shuttleXform);
        beacon.Comp.ReturnAngle = _transform.GetWorldRotation(shuttleUid);
    }

    private bool IsBeaconOnStargatePlanet(TransformComponent beaconXform)
    { return beaconXform.MapUid is { } mapUid && HasComp<StargateDestinationComponent>(mapUid); }

    public bool HasBoundShuttleOnMap(EntityUid mapUid)
    {
        var query = EntityQueryEnumerator<StarGateLandingBeaconComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.BoundShuttle is not { } shuttleUid || Deleted(shuttleUid)) continue;
            if (!_xformQuery.TryGetComponent(shuttleUid, out var shuttleXform) || shuttleXform.MapUid != mapUid) continue;
            if (HasComp<FTLComponent>(shuttleUid)) continue;
            return true;
        }
        return false;
    }

    public bool MapHasValidPlayers(EntityUid mapUid)
    {
        if (!_xformQuery.TryGetComponent(mapUid, out var mapXform)) return false;
        return RecursiveHasValidPlayer(mapXform);
    }

    private bool RecursiveHasValidPlayer(TransformComponent xform)
    {
        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (_actorQuery.TryGetComponent(child, out var actor) && !_ghostQuery.HasComp(child) && actor.PlayerSession.Status == SessionStatus.InGame && (!_mobStateQuery.TryGetComponent(child, out var mobState) || mobState.CurrentState != MobState.Dead)) return true;
            if (_xformQuery.TryGetComponent(child, out var childXform) && RecursiveHasValidPlayer(childXform)) return true;
        }
        return false;
    }

    private bool IsLandingTargetAllowed(EntityUid landingShuttleUid, MapCoordinates targetCoords, Angle targetAngle, TransformComponent beaconXform, out string blockedLocKey)
    {
        var beaconMapPos = _transform.ToMapCoordinates(beaconXform.Coordinates);
        if (Vector2.Distance(targetCoords.Position, beaconMapPos.Position) <= BeaconBlockRadius)
        {
            blockedLocKey = "stargate-shuttle-beacon-blocked-beacon";
            return false;
        }
        var entityLookup = EntityManager.System<EntityLookupSystem>();
        var nearGates = new HashSet<Entity<StargateComponent>>();
        entityLookup.GetEntitiesInRange(targetCoords, GateBlockRadius, nearGates);
        if (nearGates.Count > 0)
        {
            blockedLocKey = "stargate-shuttle-beacon-blocked-gate";
            return false;
        }
        var nearConsoles = new HashSet<Entity<StargateConsoleComponent>>();
        entityLookup.GetEntitiesInRange(targetCoords, ConsoleBlockRadius, nearConsoles);
        if (nearConsoles.Count > 0)
        {
            blockedLocKey = "stargate-shuttle-beacon-blocked-console";
            return false;
        }
        if (IntersectsOtherShuttleGrid(landingShuttleUid, targetCoords.MapId, targetCoords.Position, targetAngle))
        {
            blockedLocKey = "stargate-shuttle-beacon-blocked-shuttle";
            return false;
        }
        blockedLocKey = string.Empty;
        return true;
    }

    private bool IntersectsOtherShuttleGrid(EntityUid landingShuttleUid, MapId mapId, Vector2 landingWorldPos, Angle landingAngle)
    {
        if (!_gridQuery.TryComp(landingShuttleUid, out var landingGrid)) return false;
        var landingCorners = BuildWorldCorners(landingGrid.LocalAABB, landingWorldPos, landingAngle);
        var landingAabb = BuildAabb(landingCorners);
        _gridIntersectionBuffer.Clear();
        _mapManager.FindGridsIntersecting(mapId, landingAabb, ref _gridIntersectionBuffer);
        foreach (var otherGrid in _gridIntersectionBuffer)
        {
            var otherUid = otherGrid.Owner;
            if (otherUid == landingShuttleUid) continue;
            if (!HasComp<ShuttleComponent>(otherUid)) continue;
            if (HasComp<FTLComponent>(otherUid)) continue;
            if (!_xformQuery.TryComp(otherUid, out var otherXform)) continue;
            if (otherXform.MapID != mapId) continue;
            var otherPos = _transform.GetWorldPosition(otherXform);
            var otherAngle = _transform.GetWorldRotation(otherXform);
            var otherCorners = BuildWorldCorners(otherGrid.Comp.LocalAABB, otherPos, otherAngle);
            if (OrientedBoxesIntersect(landingCorners, otherCorners)) return true;
        }
        return false;
    }

    private static Vector2[] BuildWorldCorners(Box2 localAabb, Vector2 worldPos, Angle worldAngle)
    {
        var bottomLeft = worldPos + worldAngle.RotateVec(localAabb.BottomLeft);
        var bottomRight = worldPos + worldAngle.RotateVec(new Vector2(localAabb.Right, localAabb.Bottom));
        var topRight = worldPos + worldAngle.RotateVec(localAabb.TopRight);
        var topLeft = worldPos + worldAngle.RotateVec(new Vector2(localAabb.Left, localAabb.Top));
        return new[] { bottomLeft, bottomRight, topRight, topLeft };
    }

    private static Box2 BuildAabb(IReadOnlyList<Vector2> corners)
    {
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        for (var i = 0; i < corners.Count; i++)
        {
            var p = corners[i];
            minX = MathF.Min(minX, p.X);
            minY = MathF.Min(minY, p.Y);
            maxX = MathF.Max(maxX, p.X);
            maxY = MathF.Max(maxY, p.Y);
        }
        return new Box2(minX, minY, maxX, maxY);
    }

    private static bool OrientedBoxesIntersect(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
    { return !HasSeparatingAxis(a, b) && !HasSeparatingAxis(b, a); }

    private static bool HasSeparatingAxis(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
    {
        for (var i = 0; i < a.Count; i++)
        {
            var j = (i + 1) % a.Count;
            var edge = a[j] - a[i];
            var axis = new Vector2(-edge.Y, edge.X);
            var lengthSq = axis.LengthSquared();
            if (lengthSq <= 0.0001f) continue;
            axis /= MathF.Sqrt(lengthSq);
            ProjectPolygon(a, axis, out var minA, out var maxA);
            ProjectPolygon(b, axis, out var minB, out var maxB);
            if (maxA < minB || maxB < minA) return true;
        }
        return false;
    }

    private static void ProjectPolygon(IReadOnlyList<Vector2> poly, Vector2 axis, out float min, out float max)
    {
        var dot = Vector2.Dot(poly[0], axis);
        min = dot;
        max = dot;
        for (var i = 1; i < poly.Count; i++)
        {
            dot = Vector2.Dot(poly[i], axis);
            if (dot < min) min = dot;
            if (dot > max) max = dot;
        }
    }
}
