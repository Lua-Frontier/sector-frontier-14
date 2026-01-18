using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server._NF.Shuttles.Components; // Frontier: FTL knockdown immunity
using Content.Server._Lua.NoShuttleFTL;
using Content.Server.Emp; // Lua
using Content.Server._Lua.Starmap.Components; // Lua Warp transit marker
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Events;
using Content.Shared.Body.Components;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared._NF.Emp.Components; // Lua
using Content.Shared.Maps;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Timing;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using FTLMapComponent = Content.Shared.Shuttles.Components.FTLMapComponent;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    /*
     * This is a way to move a shuttle from one location to another, via an intermediate map for fanciness.
     */

    private readonly SoundSpecifier _startupSound = new SoundPathSpecifier("/Audio/_Lua/Effects/Shuttle/hyperspace_begin.ogg") //Lua edit
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    private readonly SoundSpecifier _arrivalSound = new SoundPathSpecifier("/Audio/_Lua/Effects/Shuttle/hyperspace_end.ogg") //Lua edit
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    public float DefaultStartupTime;
    public float DefaultTravelTime;
    public float DefaultArrivalTime;
    private float FTLCooldown;
    public float FTLMassLimit;
    private TimeSpan _hyperspaceKnockdownTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Left-side of the station we're allowed to use
    /// </summary>
    private float _index;

    /// <summary>
    /// Space between grids within hyperspace.
    /// </summary>
    private const float Buffer = 500f; // Frontier: 5 < 500

    /// <summary>
    /// How many times we try to proximity warp close to something before falling back to map-wideAABB.
    /// </summary>
    private const int FTLProximityIterations = 15; // Frontier: 5<15

    // Frontier: coordinate rollover
    /// <summary>
    /// Maximum X coordinate before rolling over.
    /// </summary>
    private const float MaxCoord = 20000f;

    /// <summary>
    /// Amount to subtract from X coordinate on rollover.
    /// </summary>
    private const float CoordRollover = 40000f;
    // End Frontier: coordinate rollover

    // Lua start: fallback
    private const float MaxWorldRadius = 30_000f;
    private const float SafeWorldRadius = 25_000f;
    // Lua end: fallback

    private readonly HashSet<EntityUid> _lookupEnts = new();
    private readonly HashSet<EntityUid> _immuneEnts = new();
    private readonly HashSet<Entity<NoFTLComponent>> _noFtls = new();

    private EntityQuery<BodyComponent> _bodyQuery;
    private EntityQuery<FTLSmashImmuneComponent> _immuneQuery;
    private EntityQuery<StatusEffectsComponent> _statusQuery;

    private void InitializeFTL()
    {
        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit);
        SubscribeLocalEvent<FTLComponent, ComponentShutdown>(OnFtlShutdown);

        _bodyQuery = GetEntityQuery<BodyComponent>();
        _immuneQuery = GetEntityQuery<FTLSmashImmuneComponent>();
        _statusQuery = GetEntityQuery<StatusEffectsComponent>();

        _cfg.OnValueChanged(CCVars.FTLStartupTime, time => DefaultStartupTime = time, true);
        _cfg.OnValueChanged(CCVars.FTLTravelTime, time => DefaultTravelTime = time, true);
        _cfg.OnValueChanged(CCVars.FTLArrivalTime, time => DefaultArrivalTime = time, true);
        _cfg.OnValueChanged(CCVars.FTLCooldown, time => FTLCooldown = time, true);
        _cfg.OnValueChanged(CCVars.FTLMassLimit, time => FTLMassLimit = time, true);
        _cfg.OnValueChanged(CCVars.HyperspaceKnockdownTime, time => _hyperspaceKnockdownTime = TimeSpan.FromSeconds(time), true);
    }

    private void OnFtlShutdown(Entity<FTLComponent> ent, ref ComponentShutdown args)
    {
        QueueDel(ent.Comp.VisualizerEntity);
        ent.Comp.VisualizerEntity = null;
    }

    private void OnStationPostInit(ref StationPostInitEvent ev)
    {
        // Add all grid maps as ftl destinations that anyone can FTL to.
        foreach (var gridUid in ev.Station.Comp.Grids)
        {
            var gridXform = _xformQuery.GetComponent(gridUid);

            if (gridXform.MapUid == null)
            {
                continue;
            }

            TryAddFTLDestination(gridXform.MapID, true, false, false, out _);
        }
    }

    /// <summary>
    /// Ensures the FTL map exists and returns it.
    /// </summary>
    private EntityUid EnsureFTLMap()
    {
        var query = AllEntityQuery<FTLMapComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            return uid;
        }

        var mapUid = _mapSystem.CreateMap(out var mapId);
        var ftlMap = AddComp<FTLMapComponent>(mapUid);

        _metadata.SetEntityName(mapUid, "FTL");
        Log.Debug($"Setup hyperspace map at {mapUid}");
        DebugTools.Assert(!_mapSystem.IsPaused(mapId));
        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = ftlMap.Parallax;

        return mapUid;
    }

    public StartEndTime GetStateTime(FTLComponent component)
    {
        var state = component.State;

        switch (state)
        {
            case FTLState.Starting:
            case FTLState.Travelling:
            case FTLState.Arriving:
            case FTLState.Cooldown:
                return component.StateTime;
            case FTLState.Available:
                return default;
            default:
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Updates the whitelist for this FTL destination.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="whitelist"></param>
    public void SetFTLWhitelist(Entity<FTLDestinationComponent?> entity, EntityWhitelist? whitelist)
    {
        if (!Resolve(entity, ref entity.Comp))
            return;

        if (entity.Comp.Whitelist == whitelist)
            return;

        entity.Comp.Whitelist = whitelist;
        _console.RefreshShuttleConsoles();
        Dirty(entity);
    }

    /// <summary>
    /// Adds the target map as available for FTL.
    /// </summary>
    public bool TryAddFTLDestination(MapId mapId, bool enabled, [NotNullWhen(true)] out FTLDestinationComponent? component)
    {
        return TryAddFTLDestination(mapId, enabled, true, false, out component);
    }

    public bool TryAddFTLDestination(MapId mapId, bool enabled, bool requireDisk, bool beaconsOnly, [NotNullWhen(true)] out FTLDestinationComponent? component)
    {
        var mapUid = _mapSystem.GetMapOrInvalid(mapId);
        component = null;

        if (!Exists(mapUid))
            return false;

        component = EnsureComp<FTLDestinationComponent>(mapUid);

        if (component.Enabled == enabled && component.RequireCoordinateDisk == requireDisk && component.BeaconsOnly == beaconsOnly)
            return true;

        component.Enabled = enabled;
        component.RequireCoordinateDisk = requireDisk;
        component.BeaconsOnly = beaconsOnly;

        _console.RefreshShuttleConsoles();
        Dirty(mapUid, component);
        return true;
    }

    [PublicAPI]
    public void RemoveFTLDestination(EntityUid uid)
    {
        if (!RemComp<FTLDestinationComponent>(uid))
            return;

        _console.RefreshShuttleConsoles();
    }

    /// <summary>
    /// Returns true if the grid can FTL. Used to block protected shuttles like the emergency shuttle.
    /// </summary>
    public bool CanFTL(EntityUid shuttleUid, [NotNullWhen(false)] out string? reason)
    {
        if (HasComp<NoShuttleFTLComponent>(shuttleUid))
        {
            reason = Loc.GetString("shuttle-console-noftl");
            return false;
        }

        // Currently in FTL already
        if (HasComp<FTLComponent>(shuttleUid))
        {
            reason = Loc.GetString("shuttle-console-in-ftl");
            return false;
        }

        if (TryComp<PhysicsComponent>(shuttleUid, out var shuttlePhysics))
        {

            // Too large to FTL
            if (FTLMassLimit > 0 && shuttlePhysics.Mass > FTLMassLimit)
            {
                reason = Loc.GetString("shuttle-console-mass");
                return false;
            }
        }

        if (HasComp<PreventPilotComponent>(shuttleUid) || HasComp<PreventFTLComponent>(shuttleUid))
        {
            reason = Loc.GetString("shuttle-console-prevent");
            return false;
        }

        if (_xformQuery.TryGetComponent(shuttleUid, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(xform);
            if (worldPos.Length() > 30000f)
            {
                reason = Loc.GetString("shuttle-console-noftl");
                return false;
            }
        }

        var ev = new ConsoleFTLAttemptEvent(shuttleUid, false, string.Empty);
        RaiseLocalEvent(shuttleUid, ref ev, true);

        if (ev.Cancelled)
        {
            reason = ev.Reason;
            return false;
        }

        reason = null;
        return true;
    }

    private bool CanFTLAsDockedCargo(EntityUid shuttleUid, [NotNullWhen(false)] out string? reason) // Lua
    {
        if (HasComp<NoShuttleFTLComponent>(shuttleUid))
        {
            reason = Loc.GetString("shuttle-console-noftl");
            return false;
        }
        if (HasComp<FTLComponent>(shuttleUid))
        {
            reason = Loc.GetString("shuttle-console-in-ftl");
            return false;
        }
        if (HasComp<PreventPilotComponent>(shuttleUid) || HasComp<PreventFTLComponent>(shuttleUid))
        {
            reason = Loc.GetString("shuttle-console-prevent");
            return false;
        }
        if (_xformQuery.TryGetComponent(shuttleUid, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(xform);
            if (worldPos.Length() > 30000f)
            {
                reason = Loc.GetString("shuttle-console-noftl");
                return false;
            }
        }
        var ev = new ConsoleFTLAttemptEvent(shuttleUid, false, string.Empty);
        RaiseLocalEvent(shuttleUid, ref ev, true);
        if (ev.Cancelled)
        {
            reason = ev.Reason;
            return false;
        }
        reason = null;
        return true;
    }

    /// <summary>
    /// Moves a shuttle from its current position to the target one without any checks. Goes through the hyperspace map while the timer is running.
    /// </summary>
    public void FTLToCoordinates(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityCoordinates coordinates,
        Angle angle,
        float? startupTime = null,
        float? hyperspaceTime = null,
        string? priorityTag = null)
    {
        if (!TrySetupFTL(shuttleUid, component, out var hyperspace))
            return;

        startupTime ??= DefaultStartupTime;
        hyperspaceTime ??= DefaultTravelTime;

        hyperspace.StartupTime = startupTime.Value;
        hyperspace.TravelTime = hyperspaceTime.Value;
        hyperspace.StateTime = StartEndTime.FromStartDuration(
            _gameTiming.CurTime,
            TimeSpan.FromSeconds(hyperspace.StartupTime));
        hyperspace.TargetCoordinates = coordinates;
        hyperspace.TargetAngle = angle;
        hyperspace.PriorityTag = priorityTag;

        if (TryGetFTLDrive(shuttleUid, out _, out var driveComp)) // Lua start
        {
            hyperspace.SkipHyperspace = driveComp.SkipHyperspace;
            hyperspace.SkipHyperspaceEmpRange = driveComp.SkipHyperspaceEmpRange;
        } // Lua end

        _console.RefreshShuttleConsoles(shuttleUid);

        var mapId = _transform.GetMapId(coordinates);
        var mapUid = _mapSystem.GetMap(mapId);
        var ev = new FTLRequestEvent(mapUid);
        RaiseLocalEvent(shuttleUid, ref ev, true);
    }

    /// <summary>
    /// Moves a shuttle from its current position to docked on the target one.
    /// If no docks are free when FTLing it will arrive in proximity
    /// </summary>
    public void FTLToDock(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityUid target,
        float? startupTime = null,
        float? hyperspaceTime = null,
        string? priorityTag = null)
    {
        if (!TrySetupFTL(shuttleUid, component, out var hyperspace))
            return;

        startupTime ??= DefaultStartupTime;
        hyperspaceTime ??= DefaultTravelTime;

        var config = _dockSystem.GetDockingConfig(shuttleUid, target, priorityTag);
        hyperspace.StartupTime = startupTime.Value;
        hyperspace.TravelTime = hyperspaceTime.Value;
        hyperspace.StateTime = StartEndTime.FromStartDuration(
            _gameTiming.CurTime,
            TimeSpan.FromSeconds(hyperspace.StartupTime));
        hyperspace.PriorityTag = priorityTag;

        if (TryGetFTLDrive(shuttleUid, out _, out var driveComp)) // Lua start
        {
            hyperspace.SkipHyperspace = driveComp.SkipHyperspace;
            hyperspace.SkipHyperspaceEmpRange = driveComp.SkipHyperspaceEmpRange;
        } //Lue ned

        _console.RefreshShuttleConsoles(shuttleUid);

        // Valid dock for now time so just use that as the target.
        if (config != null)
        {
            hyperspace.TargetCoordinates = config.Coordinates;
            hyperspace.TargetAngle = config.Angle;
        }
        else if (TryGetFTLProximity(shuttleUid, new EntityCoordinates(target, Vector2.Zero), out var coords, out var targAngle))
        {
            hyperspace.TargetCoordinates = coords;
            hyperspace.TargetAngle = targAngle;
        }
        else
        {
            // FTL back to its own position.
            hyperspace.TargetCoordinates = Transform(shuttleUid).Coordinates;
            Log.Error($"Unable to FTL grid {ToPrettyString(shuttleUid)} to target properly?");
        }
    }

    // FTL Mono Carrier start
    /// <summary>
    /// Recursively gets all docked shuttles to the target shuttle, ignoring <see cref="FTLLockComponent"/>.
    /// </summary>
    public void GetAllDockedShuttlesIgnoringFTLLock(EntityUid shuttleUid, HashSet<EntityUid> dockedShuttles)
    {
        if (!dockedShuttles.Add(shuttleUid))
            return; // Already processed

        var docks = _dockSystem.GetDocks(shuttleUid);
        foreach (var dock in docks)
        {
            if (!TryComp<DockingComponent>(dock, out var dockComp) || dockComp.Docked == false)
                continue;
            if (dockComp.DockedWith == null)
                continue;
            var dockedGridUid = _transform.GetParentUid(dockComp.DockedWith.Value);
            if (dockedGridUid == EntityUid.Invalid || !HasComp<ShuttleComponent>(dockedGridUid))
                continue;
            GetAllDockedShuttlesIgnoringFTLLock(dockedGridUid, dockedShuttles);
        }
    }
    // FTL Mono Carrier end

    private bool TrySetupFTL(EntityUid uid, ShuttleComponent shuttle, [NotNullWhen(true)] out FTLComponent? component)
    {
        component = null;

        if (HasComp<FTLComponent>(uid))
        {
            Log.Warning($"Tried queuing {ToPrettyString(uid)} which already has {nameof(FTLComponent)}?");
            return false;
        }

        if (_xformQuery.TryGetComponent(uid, out var xform)) // Lua start
        {
            var worldPos = _transform.GetWorldPosition(xform);
            if (worldPos.Length() > 30000f) return false;
        }  // Lua end

        _thruster.DisableLinearThrusters(shuttle);
        _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.North);
        _thruster.SetAngularThrust(shuttle, false);

        // FTL Mono Carrier start
        // Determine docked shuttles that should travel together (respecting FTLLock).
        var dockedShuttles = new HashSet<EntityUid>();
        GetAllDockedShuttles(uid, dockedShuttles);
        // Force undock emergency and arrivals shuttles.
        if (HasComp<EmergencyShuttleComponent>(uid) || HasComp<ArrivalsShuttleComponent>(uid))
        {
            _dockSystem.UndockDocks(uid);
        }
        else
        {
            foreach (var dockedUid in dockedShuttles)
            {
                if (dockedUid == uid)
                    continue;

                if (!CanFTLAsDockedCargo(dockedUid, out _)) // Lua
                    return false;
            }
            foreach (var dock in _dockSystem.GetDocks(uid))
            {
                if (!TryComp<DockingComponent>(dock, out var dockComp) || !dockComp.Docked || dockComp.DockedWith == null)
                    continue;

                var connectedEntityUid = _transform.GetParentUid(dockComp.DockedWith.Value);
                if (connectedEntityUid == EntityUid.Invalid ||
                    !HasComp<ShuttleComponent>(connectedEntityUid) ||
                    !dockedShuttles.Contains(connectedEntityUid))
                {
                    _dockSystem.Undock((dock, dockComp));
                }
            }
            foreach (var dockedUid in dockedShuttles)
            {
                if (dockedUid == uid)
                    continue;

                foreach (var dock in _dockSystem.GetDocks(dockedUid))
                {
                    if (!TryComp<DockingComponent>(dock, out var dockComp) || !dockComp.Docked || dockComp.DockedWith == null)
                        continue;

                    var connectedEntityUid = _transform.GetParentUid(dockComp.DockedWith.Value);
                    if (connectedEntityUid == EntityUid.Invalid ||
                        !dockedShuttles.Contains(connectedEntityUid))
                    {
                        _dockSystem.Undock((dock, dockComp));
                    }
                }
            }
        }
        // FTL Mono Carrier end

        component = AddComp<FTLComponent>(uid);
        component.State = FTLState.Starting;
        var audio = _audio.PlayPvs(_startupSound, uid);
        _audio.SetGridAudio(audio);
        component.StartupStream = audio?.Entity;

        // Make sure the map is setup before we leave to avoid pop-in (e.g. parallax).
        EnsureFTLMap();
        return true;
    }

    /// <summary>
    /// Transitions shuttle to FTL map.
    /// </summary>
    private void UpdateFTLStarting(Entity<FTLComponent, ShuttleComponent> entity)
    {
        var uid = entity.Owner;
        var comp = entity.Comp1;
        var xform = _xformQuery.GetComponent(entity);
        // Lua start: fallback
        var grid = Comp<MapGridComponent>(uid);
        if (!ValidateGridForFtl(uid, grid, xform))
        {
            Log.Error($"[FTL-DIAG] Aborting FTL for {ToPrettyString(uid)} on map {xform.MapID} due to invalid world AABB.");
            comp.State = FTLState.Cooldown;
            comp.StateTime = StartEndTime.FromCurTime(_gameTiming, FTLCooldown);
            _console.RefreshShuttleConsoles(uid);
            return;
        }
        // Lua end: fallback

        DoTheDinosaur(xform);

        if (comp.SkipHyperspace) // Lua start
        {
            SpawnEmpVisualOnly(_transform.GetMapCoordinates(uid), comp.SkipHyperspaceEmpRange);
            _thruster.DisableLinearThrusters(entity.Comp2);
            _thruster.EnableLinearThrustDirection(entity.Comp2, DirectionFlag.South);
            _console.RefreshShuttleConsoles(uid);
            var target = entity.Comp1.TargetCoordinates;
            MapId mapId;
            QueueDel(entity.Comp1.VisualizerEntity);
            entity.Comp1.VisualizerEntity = null;
            if (!Exists(entity.Comp1.TargetCoordinates.EntityId))
            {
                var maps = EntityQuery<MapComponent>().Select(o => o.MapId).ToList();
                var map = maps.Min(o => o.GetHashCode());
                mapId = new MapId(map);
                TryFTLProximity(uid, _mapSystem.GetMap(mapId));
            }
            else if (HasComp<MapGridComponent>(target.EntityId) && !HasComp<MapComponent>(target.EntityId))
            {
                var config = _dockSystem.GetDockingConfigAt(uid, target.EntityId, target, entity.Comp1.TargetAngle);
                var mapCoordinates = _transform.ToMapCoordinates(target);
                if (config == null) { TryFTLProximity(uid, target.EntityId); }
                else { FTLDock((uid, xform), config); }
                mapId = mapCoordinates.MapId;
            }
            else
            {
                mapId = _transform.GetMapId(target);
                _transform.SetCoordinates(uid, xform, target, rotation: entity.Comp1.TargetAngle);
            }
            if (_physicsQuery.TryGetComponent(uid, out var bodyImm))
            {
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: bodyImm);
                _physics.SetAngularVelocity(uid, 0f, body: bodyImm);
                if (HasComp<MapGridComponent>(xform.MapUid)) { Disable(uid, component: bodyImm); }
                else { Enable(uid, component: bodyImm, shuttle: entity.Comp2); }
            }
            _thruster.DisableLinearThrusters(entity.Comp2);
            var audio = _audio.PlayPvs(_arrivalSound, uid);
            _audio.SetGridAudio(audio);
            SpawnEmpVisualOnly(_transform.ToMapCoordinates(target), comp.SkipHyperspaceEmpRange);
            if (TryComp<FTLDestinationComponent>(uid, out var dest))
            { dest.Enabled = true; }
            comp.State = FTLState.Cooldown;
            comp.StateTime = StartEndTime.FromCurTime(_gameTiming, FTLCooldown);
            _console.RefreshShuttleConsoles(uid);
            _mapSystem.SetPaused(mapId, false);
            Smimsh(uid, xform: xform);
            var ftlEvent = new FTLCompletedEvent(uid, _mapSystem.GetMap(mapId));
            RaiseLocalEvent(uid, ref ftlEvent, true); return;
        } // Lua end

        comp.State = FTLState.Travelling;
        var fromMapUid = xform.MapUid;
        var fromMatrix = _transform.GetWorldMatrix(xform);
        var fromRotation = _transform.GetWorldRotation(xform);

        var width = grid.LocalAABB.Width;
        var ftlMap = EnsureFTLMap();
        var body = _physicsQuery.GetComponent(entity);
        var shuttleCenter = grid.LocalAABB.Center;

        // FTL Mono Carrier start
        // Move docked shuttles into hyperspace while keeping their relative transforms to the main shuttle.
        var dockedShuttles = new HashSet<EntityUid>();
        GetAllDockedShuttles(uid, dockedShuttles);

        var relativeTransforms = new Dictionary<EntityUid, (Vector2 Position, Angle Rotation)>();
        var mainPos = _transform.GetWorldPosition(uid);
        var mainRot = _transform.GetWorldRotation(uid);
        foreach (var dockedUid in dockedShuttles)
        {
            if (dockedUid == uid)
                continue;

            var dockedPos = _transform.GetWorldPosition(dockedUid);
            var dockedRot = _transform.GetWorldRotation(dockedUid);

            var relativePos = dockedPos - mainPos;
            relativePos = (-mainRot).RotateVec(relativePos);
            var relativeRot = dockedRot - mainRot;
            relativeTransforms[dockedUid] = (relativePos, relativeRot);
        }
        // FTL Mono Carrier end

        // Leave audio at the old spot
        // Just so we don't clip
        if (fromMapUid != null && TryComp(comp.StartupStream, out AudioComponent? startupAudio))
        {
            var clippedAudio = _audio.PlayStatic(_startupSound, Filter.Broadcast(),
                new EntityCoordinates(fromMapUid.Value, _mapSystem.GetGridPosition(entity.Owner)), true, startupAudio.Params);

            _audio.SetPlaybackPosition(clippedAudio, entity.Comp1.StartupTime);
            if (clippedAudio != null)
                clippedAudio.Value.Component.Flags |= AudioFlags.NoOcclusion;
        }

        // Offset the start by buffer range just to avoid overlap.
        var ftlStart = new EntityCoordinates(ftlMap, new Vector2(_index + width / 2f, 0f) - shuttleCenter);

        // Store the matrix for the grid prior to movement. This means any entities we need to leave behind we can make sure their positions are updated.
        // Setting the entity to map directly may run grid traversal (at least at time of writing this).
        var oldMapUid = xform.MapUid;
        var oldGridMatrix = _transform.GetWorldMatrix(xform);
        _transform.SetCoordinates(entity.Owner, ftlStart);
        _transform.SetWorldRotation(entity.Owner, Angle.Zero); // FTL Mono Carrier
        LeaveNoFTLBehind((entity.Owner, xform), oldGridMatrix, oldMapUid);

        // Reset rotation so they always face the same direction.
        xform.LocalRotation = Angle.Zero;
        _index += width + Buffer;
        comp.StateTime = StartEndTime.FromCurTime(_gameTiming, comp.TravelTime - DefaultArrivalTime);

        // Frontier: rollover coordinates
        if (_index > MaxCoord)
            _index -= CoordRollover;
        // End Frontier

        // FTL Mono Carrier start
        // Apply the same relative transforms for all docked shuttles in hyperspace.
        var mainNewPos = _transform.GetWorldPosition(uid);
        var mainNewRot = _transform.GetWorldRotation(uid);

        foreach (var dockedUid in dockedShuttles)
        {
            if (dockedUid == uid)
                continue;

            var dockedXform = _xformQuery.GetComponent(dockedUid);
            var dockedOldMapUid = dockedXform.MapUid;
            var dockedOldGridMatrix = _transform.GetWorldMatrix(dockedXform);
            var (relativePos, relativeRot) = relativeTransforms[dockedUid];

            var rotatedRelativePos = mainNewRot.RotateVec(relativePos);
            var newPos = mainNewPos + rotatedRelativePos;
            var newRot = mainNewRot + relativeRot;

            _transform.SetParent(dockedUid, dockedXform, ftlMap);
            _transform.SetWorldRotationNoLerp(dockedUid, newRot);
            _transform.SetWorldPosition(dockedUid, newPos);
            LeaveNoFTLBehind((dockedUid, dockedXform), dockedOldGridMatrix, dockedOldMapUid);

            // Mark as linked so only the main shuttle drives FTL state machine.
            var dockedComp = EnsureComp<FTLComponent>(dockedUid);
            dockedComp.LinkedShuttle = uid;
            dockedComp.State = FTLState.Travelling;
            dockedComp.StateTime = comp.StateTime;
            dockedComp.TargetAngle = comp.TargetAngle + relativeRot;

            // Keep docked shuttles from drifting apart in hyperspace: match physics state with the carrier. // FTL Mono Carrier
            if (_physicsQuery.TryGetComponent(dockedUid, out var dockedBody))
            {
                Enable(dockedUid, component: dockedBody);
                _physics.SetLinearVelocity(dockedUid, new Vector2(0f, 20f), body: dockedBody);
                _physics.SetAngularVelocity(dockedUid, 0f, body: dockedBody);
            }

            _console.RefreshShuttleConsoles(dockedUid);
        }
        // FTL Mono Carrier end

        Enable(uid, component: body);
        _physics.SetLinearVelocity(uid, new Vector2(0f, 20f), body: body);
        _physics.SetAngularVelocity(uid, 0f, body: body);

        _dockSystem.SetDockBolts(uid, true);
        _console.RefreshShuttleConsoles(uid);

        var ev = new FTLStartedEvent(uid, comp.TargetCoordinates, fromMapUid, fromMatrix, fromRotation);
        RaiseLocalEvent(uid, ref ev, true);

        // Audio
        var wowdio = _audio.PlayPvs(comp.TravelSound, uid);
        comp.TravelStream = wowdio?.Entity;
        _audio.SetGridAudio(wowdio);
    }

    /// <summary>
    /// Shuttle arriving.
    /// </summary>
    private void UpdateFTLTravelling(Entity<FTLComponent, ShuttleComponent> entity)
    {
        // FTL Mono Carrier start
        // Linked shuttles are moved/handled by the main shuttle.
        if (entity.Comp1.LinkedShuttle.HasValue)
            return;
        // FTL Mono Carrier end

        var shuttle = entity.Comp2;
        var comp = entity.Comp1;
        comp.StateTime = StartEndTime.FromCurTime(_gameTiming, DefaultArrivalTime);
        comp.State = FTLState.Arriving;

        if (entity.Comp1.VisualizerProto != null)
        {
            comp.VisualizerEntity = SpawnAttachedTo(entity.Comp1.VisualizerProto, entity.Comp1.TargetCoordinates);
            DebugTools.Assert(Transform(comp.VisualizerEntity.Value).ParentUid == entity.Comp1.TargetCoordinates.EntityId);
            var visuals = Comp<FtlVisualizerComponent>(comp.VisualizerEntity.Value);
            visuals.Grid = entity.Owner;
            Dirty(comp.VisualizerEntity.Value, visuals);
            _transform.SetLocalRotation(comp.VisualizerEntity.Value, entity.Comp1.TargetAngle);
            _pvs.AddGlobalOverride(comp.VisualizerEntity.Value);
        }

        _thruster.DisableLinearThrusters(shuttle);
        _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.South);

        _console.RefreshShuttleConsoles(entity.Owner);
    }

    /// <summary>
    ///  Shuttle arrived.
    /// </summary>
    private void UpdateFTLArriving(Entity<FTLComponent, ShuttleComponent> entity)
    {
        var uid = entity.Owner;
        var xform = _xformQuery.GetComponent(uid);
        var body = _physicsQuery.GetComponent(uid);
        var comp = entity.Comp1;

        // FTL Mono Carrier start
        // Linked shuttles are handled by their main shuttle.
        if (comp.LinkedShuttle.HasValue)
            return;
        // FTL Mono Carrier end

        DoTheDinosaur(xform);
        _dockSystem.SetDockBolts(entity, false);

        _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
        _physics.SetAngularVelocity(uid, 0f, body: body);

        // FTL Mono Carrier start
        // Capture relative transforms + docking connections for all docked shuttles before moving the main shuttle.
        var dockedShuttles = new HashSet<EntityUid>();
        GetAllDockedShuttles(uid, dockedShuttles);

        var relativeTransforms = new Dictionary<EntityUid, (Vector2 Position, Angle Rotation, List<(EntityUid DockA, EntityUid DockB)> Docks)>();
        var preMoveMainPos = _transform.GetWorldPosition(uid);
        var preMoveMainRot = _transform.GetWorldRotation(uid);

        foreach (var dockedUid in dockedShuttles)
        {
            if (dockedUid == uid)
                continue;

            var dockedPos = _transform.GetWorldPosition(dockedUid);
            var dockedRot = _transform.GetWorldRotation(dockedUid);

            var relativePos = dockedPos - preMoveMainPos;
            relativePos = (-preMoveMainRot).RotateVec(relativePos);
            var relativeRot = dockedRot - preMoveMainRot;

            // Record docking connections from this shuttle and undock them so we can safely reposition.
            var dockConnections = new List<(EntityUid DockA, EntityUid DockB)>();
            foreach (var dock in _dockSystem.GetDocks(dockedUid))
            {
                if (!TryComp<DockingComponent>(dock, out var dockComp) || !dockComp.Docked || dockComp.DockedWith == null)
                    continue;

                dockConnections.Add((dock, dockComp.DockedWith.Value));
                _dockSystem.Undock((dock, dockComp));
            }

            relativeTransforms[dockedUid] = (relativePos, relativeRot, dockConnections);

            if (_physicsQuery.TryGetComponent(dockedUid, out var dockedBody))
            {
                _physics.SetLinearVelocity(dockedUid, Vector2.Zero, body: dockedBody);
                _physics.SetAngularVelocity(dockedUid, 0f, body: dockedBody);
            }
        }
        // FTL Mono Carrier end

        var target = entity.Comp1.TargetCoordinates;

        if (comp.SkipHyperspace) { SpawnEmpVisualOnly(_transform.ToMapCoordinates(target), 60f); } // Lua

        MapId mapId;

        QueueDel(entity.Comp1.VisualizerEntity);
        entity.Comp1.VisualizerEntity = null;

        if (!Exists(entity.Comp1.TargetCoordinates.EntityId))
        {
            // Uhh good luck
            // Pick earliest map?
            var maps = EntityQuery<MapComponent>().Select(o => o.MapId).ToList();
            var map = maps.Min(o => o.GetHashCode());

            mapId = new MapId(map);
            TryFTLProximity(uid, _mapSystem.GetMap(mapId));
        }
        // Docking FTL
        else if (HasComp<MapGridComponent>(target.EntityId) &&
                 !HasComp<MapComponent>(target.EntityId))
        {
            var config = _dockSystem.GetDockingConfigAt(uid, target.EntityId, target, entity.Comp1.TargetAngle);
            var mapCoordinates = _transform.ToMapCoordinates(target);

            // Couldn't dock somehow so just fallback to regular position FTL.
            if (config == null)
            {
                TryFTLProximity(uid, target.EntityId);
            }
            else
            {
                FTLDock((uid, xform), config);
            }

            mapId = mapCoordinates.MapId;
        }
        // Position ftl
        else
        {
            // TODO: This should now use tryftlproximity
            mapId = _transform.GetMapId(target);
            _transform.SetCoordinates(uid, xform, target, rotation: entity.Comp1.TargetAngle);
        }

        // FTL Mono Carrier start
        // Move all docked shuttles to maintain relative transforms, then re-establish their docking connections.
        var postMoveMainPos = _transform.GetWorldPosition(uid);
        var postMoveMainRot = _transform.GetWorldRotation(uid);

        foreach (var dockedUid in dockedShuttles)
        {
            if (dockedUid == uid)
                continue;

            var dockedXform = _xformQuery.GetComponent(dockedUid);
            var (relativePos, relativeRot, dockConnections) = relativeTransforms[dockedUid];

            var newPos = postMoveMainPos + postMoveMainRot.RotateVec(relativePos);
            var newRot = postMoveMainRot + relativeRot;

            if (xform.MapUid != null)
            {
                _transform.SetParent(dockedUid, dockedXform, xform.MapUid.Value);
                _transform.SetWorldRotationNoLerp(dockedUid, newRot);
                _transform.SetWorldPosition(dockedUid, newPos);
            }

            if (_physicsQuery.TryGetComponent(dockedUid, out var dockedBody))
            {
                _physics.SetLinearVelocity(dockedUid, Vector2.Zero, body: dockedBody);
                _physics.SetAngularVelocity(dockedUid, 0f, body: dockedBody);

                var dockedShuttle = Comp<ShuttleComponent>(dockedUid);
                if (HasComp<MapGridComponent>(xform.MapUid))
                    Disable(dockedUid, component: dockedBody);
                else
                    Enable(dockedUid, component: dockedBody, shuttle: dockedShuttle);
            }

            foreach (var (dockA, dockB) in dockConnections)
            {
                if (!TryComp<DockingComponent>(dockA, out var dockCompA) ||
                    !TryComp<DockingComponent>(dockB, out var dockCompB))
                    continue;

                _dockSystem.Dock((dockA, dockCompA), (dockB, dockCompB));
            }

            // Put linked shuttles into cooldown too; the main shuttle will clear them.
            if (TryComp<FTLComponent>(dockedUid, out var dockedFtl))
            {
                dockedFtl.LinkedShuttle = uid;
                dockedFtl.State = FTLState.Cooldown;
                dockedFtl.StateTime = StartEndTime.FromCurTime(_gameTiming, FTLCooldown);
            }

            _console.RefreshShuttleConsoles(dockedUid);
        }
        // FTL Mono Carrier end

        if (_physicsQuery.TryGetComponent(uid, out body))
        {
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);

            // Disable shuttle if it's on a planet; unfortunately can't do this in parent change messages due
            // to event ordering and awake body shenanigans (at least for now).
            if (HasComp<MapGridComponent>(xform.MapUid))
            {
                Disable(uid, component: body);
            }
            else
            {
                Enable(uid, component: body, shuttle: entity.Comp2);
            }
        }

        _thruster.DisableLinearThrusters(entity.Comp2);

        comp.TravelStream = _audio.Stop(comp.TravelStream);
        var audio = _audio.PlayPvs(_arrivalSound, uid);
        _audio.SetGridAudio(audio);

        if (TryComp<FTLDestinationComponent>(uid, out var dest))
        {
            dest.Enabled = true;
        }
        comp.State = FTLState.Cooldown;
        comp.StateTime = StartEndTime.FromCurTime(_gameTiming, FTLCooldown);
        _console.RefreshShuttleConsoles(uid);
        _mapSystem.SetPaused(mapId, false);
        Smimsh(uid, xform: xform);

        var ftlEvent = new FTLCompletedEvent(uid, _mapSystem.GetMap(mapId));
        RaiseLocalEvent(uid, ref ftlEvent, true);
    }

    private void UpdateFTLCooldown(Entity<FTLComponent, ShuttleComponent> entity)
    {
        // FTL Mono Carrier start
        // Remove the main shuttle's FTL component.
        var uid = entity.Owner;
        RemCompDeferred<FTLComponent>(entity);

        // Force linked shuttles (from the same trip) to also end cooldown now.
        var linkedQuery = EntityQueryEnumerator<FTLComponent>();
        while (linkedQuery.MoveNext(out var linkedUid, out var linkedComp))
        {
            if (linkedComp.LinkedShuttle == uid && linkedComp.State == FTLState.Cooldown)
            {
                RemCompDeferred<FTLComponent>(linkedUid);
                _console.RefreshShuttleConsoles(linkedUid);
            }
        }

        _console.RefreshShuttleConsoles(uid);
        // FTL Mono Carrier end
    }

    private void UpdateHyperspace()
    {
        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<FTLComponent, ShuttleComponent>();

        while (query.MoveNext(out var uid, out var comp, out var shuttle))
        {
            // FTL Mono Carrier start
            // Linked shuttles are driven by the main shuttle; skip their state machine.
            if (comp.LinkedShuttle.HasValue)
                continue;
            // FTL Mono Carrier end

            if (curTime < comp.StateTime.End)
                continue;

            var entity = (uid, comp, shuttle);

            switch (comp.State)
            {
                // Startup time has elapsed and in hyperspace.
                case FTLState.Starting:
                    UpdateFTLStarting(entity);
                    break;
                // Arriving, play effects
                case FTLState.Travelling:
                    UpdateFTLTravelling(entity);
                    break;
                // Arrived
                case FTLState.Arriving:
                    UpdateFTLArriving(entity);
                    break;
                case FTLState.Cooldown:
                    UpdateFTLCooldown(entity);
                    break;
                default:
                    Log.Error($"Found invalid FTL state {comp.State} for {uid}");
                    RemCompDeferred<FTLComponent>(uid);
                    break;
            }
        }
    }

    // Lua start
    private void SpawnEmpVisualOnly(MapCoordinates coordinates, float range)
    {
        var empBlast = Spawn(EmpSystem.EmpPulseEffectPrototype, coordinates);
        if (EnsureComp<EmpBlastComponent>(empBlast, out var blast))
        {
            blast.VisualRange = range;
            Dirty(empBlast, blast);
        }
    }
    // Lua end

    private float GetSoundRange(EntityUid uid)
    {
        if (!TryComp<MapGridComponent>(uid, out var grid))
            return 4f;

        return MathF.Max(grid.LocalAABB.Width, grid.LocalAABB.Height) + 12.5f;
    }

    // Lua start: fallback
    private bool ValidateGridForFtl(EntityUid gridUid, MapGridComponent grid, TransformComponent xform)
    {
        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
        var aabb = grid.LocalAABB.Translated(worldPos);
        var worldAabb = new Box2Rotated(aabb, worldRot, worldPos).CalcBoundingBox();

        if (!worldAabb.IsValid() || worldAabb.HasNan())
        {
            Log.Error($"[FTL-DIAG] Invalid grid world AABB for {ToPrettyString(gridUid)} on map {xform.MapID}: {worldAabb}");
            return false;
        }

        var c = worldAabb.Center;
        if (MathF.Abs(c.X) > MaxWorldRadius ||
            MathF.Abs(c.Y) > MaxWorldRadius ||
            MathF.Abs(worldAabb.Left) > MaxWorldRadius ||
            MathF.Abs(worldAabb.Right) > MaxWorldRadius ||
            MathF.Abs(worldAabb.Top) > MaxWorldRadius ||
            MathF.Abs(worldAabb.Bottom) > MaxWorldRadius)
        {
            var length = c.Length();
            if (length > 0f)
            {
                var clampedRadius = SafeWorldRadius;
                var newCenter = c * (clampedRadius / length);
                var delta = newCenter - c;

                var current = _transform.GetMapCoordinates(gridUid);
                var newCoords = new MapCoordinates(current.Position + delta, current.MapId);

                Log.Error($"[FTL-DIAG] Repositioning grid {ToPrettyString(gridUid)} from {c} to {newCenter} before FTL (world limit {MaxWorldRadius}, safe {SafeWorldRadius}).");
                _transform.SetMapCoordinates(gridUid, newCoords);
            }
            else
            {
                Log.Error($"[FTL-DIAG] Out-of-range grid world AABB with zero-length center for {ToPrettyString(gridUid)} on map {xform.MapID}: center={c}, bounds={worldAabb} (limit={MaxWorldRadius})");
            }
        }

        return true;
    }
    // Lua end: fallback

    /// <summary>
    /// Puts everyone unbuckled on the floor, paralyzed.
    /// </summary>
    private void DoTheDinosaur(TransformComponent xform)
    {
        // Get enumeration exceptions from people dropping things if we just paralyze as we go
        var toKnock = new ValueList<EntityUid>();
        KnockOverKids(xform, ref toKnock);
        TryComp<MapGridComponent>(xform.GridUid, out var grid);

        if (TryComp<PhysicsComponent>(xform.GridUid, out var shuttleBody))
        {
            foreach (var child in toKnock)
            {
                if (!HasComp<FTLKnockdownImmuneComponent>(child)) // Frontier: FTL knockdown immunity
                    _stuns.TryUpdateParalyzeDuration(child, _hyperspaceKnockdownTime);

                // If the guy we knocked down is on a spaced tile, throw them too
                if (grid != null)
                    TossIfSpaced((xform.GridUid.Value, grid, shuttleBody), child);
            }
        }
    }

    private void LeaveNoFTLBehind(Entity<TransformComponent> grid, Matrix3x2 oldGridMatrix, EntityUid? oldMapUid)
    {
        if (oldMapUid == null)
            return;

        _noFtls.Clear();
        var oldGridRotation = oldGridMatrix.Rotation();
        _lookup.GetGridEntities(grid.Owner, _noFtls);

        foreach (var childUid in _noFtls)
        {
            if (!_xformQuery.TryComp(childUid, out var childXform))
                continue;

            // If we're not parented directly to the grid the matrix may be wrong.
            var relative = _physics.GetRelativePhysicsTransform(childUid.Owner, (grid.Owner, grid.Comp));

            _transform.SetCoordinates(
                childUid,
                childXform,
                new EntityCoordinates(oldMapUid.Value,
                Vector2.Transform(relative.Position, oldGridMatrix)), rotation: relative.Quaternion2D.Angle + oldGridRotation);
        }
    }

    private void KnockOverKids(TransformComponent xform, ref ValueList<EntityUid> toKnock)
    {
        // Not recursive because probably not necessary? If we need it to be that's why this method is separate.
        var childEnumerator = xform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (!_buckleQuery.TryGetComponent(child, out var buckle) || buckle.Buckled)
                continue;

            toKnock.Add(child);
        }
    }

    /// <summary>
    /// Throws people who are standing on a spaced tile, tries to throw them towards a neighbouring space tile
    /// </summary>
    private void TossIfSpaced(Entity<MapGridComponent, PhysicsComponent> shuttleEntity, EntityUid tossed)
    {
        var shuttleGrid = shuttleEntity.Comp1;
        var shuttleBody = shuttleEntity.Comp2;
        if (!_xformQuery.TryGetComponent(tossed, out var childXform))
            return;

        // only toss if its on lattice/space
        var tile = _mapSystem.GetTileRef(shuttleEntity, shuttleGrid, childXform.Coordinates);

        if (!_turf.IsSpace(tile))
            return;

        var throwDirection = childXform.LocalPosition - shuttleBody.LocalCenter;

        if (throwDirection == Vector2.Zero)
            return;

        _throwing.TryThrow(tossed, throwDirection.Normalized() * 10.0f, 50.0f);
    }

    /// <summary>
    /// Tries to dock with the target grid, otherwise falls back to proximity.
    /// This bypasses FTL travel time.
    /// </summary>
    public bool TryFTLDock(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityUid targetUid,
        string? priorityTag = null,
        DockType dockType = DockType.Airlock) // Frontier
    {
        return TryFTLDock(shuttleUid, component, targetUid, out _, priorityTag, dockType); // Frontier: add dockType
    }

    /// <summary>
    /// Tries to dock with the target grid, otherwise falls back to proximity.
    /// This bypasses FTL travel time.
    /// </summary>
    public bool TryFTLDock(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityUid targetUid,
        [NotNullWhen(true)] out DockingConfig? config,
        string? priorityTag = null,
        DockType dockType = DockType.Airlock) // Frontier
    {
        config = null;

        if (!_xformQuery.TryGetComponent(shuttleUid, out var shuttleXform) ||
            !_xformQuery.TryGetComponent(targetUid, out var targetXform) ||
            targetXform.MapUid == null ||
            !targetXform.MapUid.Value.IsValid())
        {
            return false;
        }

        config = _dockSystem.GetDockingConfig(shuttleUid, targetUid, priorityTag, dockType); // Frontier: add dockType

        if (config != null)
        {
            FTLDock((shuttleUid, shuttleXform), config);
            return true;
        }

        TryFTLProximity(shuttleUid, targetUid, shuttleXform, targetXform);
        return false;
    }

    /// <summary>
    /// Forces an FTL dock.
    /// </summary>
    public void FTLDock(Entity<TransformComponent> shuttle, DockingConfig config)
    {
        // Set position
        var mapCoordinates = _transform.ToMapCoordinates(config.Coordinates);
        var mapUid = _mapSystem.GetMap(mapCoordinates.MapId);
        _transform.SetCoordinates(shuttle.Owner, shuttle.Comp, new EntityCoordinates(mapUid, mapCoordinates.Position), rotation: config.Angle + _transform.GetWorldRotation(config.Coordinates.EntityId));

        // Connect everything
        foreach (var (dockAUid, dockBUid, dockA, dockB) in config.Docks)
        {
            _dockSystem.Dock((dockAUid, dockA), (dockBUid, dockB));
        }
    }

    /// <summary>
    /// Tries to get the target position to FTL near the target coordinates.
    /// If the target coordinates have a mapgrid then will try to offset the AABB.
    /// </summary>
    /// <param name="minOffset">Min offset for the final FTL.</param>
    /// <param name="maxOffset">Max offset for the final FTL from the box we spawn.</param>
    private bool TryGetFTLProximity(
        EntityUid shuttleUid,
        EntityCoordinates targetCoordinates,
        out EntityCoordinates coordinates, out Angle angle,
        float minOffset = 0f, float maxOffset = 64f,
        TransformComponent? xform = null, TransformComponent? targetXform = null)
    {
        DebugTools.Assert(minOffset < maxOffset);
        coordinates = EntityCoordinates.Invalid;
        angle = Angle.Zero;

        if (!Resolve(targetCoordinates.EntityId, ref targetXform) ||
            targetXform.MapUid == null ||
            !targetXform.MapUid.Value.IsValid() ||
            !Resolve(shuttleUid, ref xform))
        {
            return false;
        }

        // We essentially expand the Box2 of the target area until nothing else is added then we know it's valid.
        // Can't just get an AABB of every grid as we may spawn very far away.
        //var nearbyGrids = new HashSet<EntityUid>(); // Frontier
        var shuttleAABB = Comp<MapGridComponent>(shuttleUid).LocalAABB;

        // Start with small point.
        // If our target pos is offset we mot even intersect our target's AABB so we don't include it.
        var targetLocalAABB = Box2.CenteredAround(targetCoordinates.Position, Vector2.One);

        // How much we expand the target AABB be.
        // We half it because we only need the width / height in each direction if it's placed at a particular spot.
        var expansionAmount = MathF.Max(shuttleAABB.Width * 0.72f, shuttleAABB.Height * 0.72f); // Frontier: "/ 2" < "* 0.72" - a bit over sqrt 2, worst case for AABB shenanigans

        // Expand the starter AABB so we have something to query to start with.
        var targetAABB = _transform.GetWorldMatrix(targetXform)
            .TransformBox(targetLocalAABB)
            .Enlarged(expansionAmount);

        // Frontier: our world is very dense in places, very sparse overall, and very large.
        // Running a mapwise union results in ships sent very far away.
        var iteration = 0;
        var grids = new List<Entity<MapGridComponent>>();
        const float minMargin = 8.0f;
        const float maxMargin = 32.0f;

        // Pick a cardinal direction to move in.
        // true: axis-positive movement
        // false: axis-negative movement
        // null: no movement in axis
        var direction = _random.Next(8);
        bool? positiveX;
        bool? positiveY;
        // Nasty but readable
        switch (direction)
        {
            case 0:
            default:
                positiveX = true;
                positiveY = null;
                break;
            case 1:
                positiveX = true;
                positiveY = true;
                break;
            case 2:
                positiveX = null;
                positiveY = true;
                break;
            case 3:
                positiveX = false;
                positiveY = true;
                break;
            case 4:
                positiveX = false;
                positiveY = null;
                break;
            case 5:
                positiveX = false;
                positiveY = false;
                break;
            case 6:
                positiveX = null;
                positiveY = false;
                break;
            case 7:
                positiveX = true;
                positiveY = false;
                break;
        }
        while (iteration < FTLProximityIterations)
        {
            grids.Clear();
            _mapManager.FindGridsIntersecting(targetXform.MapID, targetAABB, ref grids);
            if (grids.Count == 0)
                break;

            // Adjust our requested position to be clear of intersecting grids along our randomly chosen direction.
            foreach (var grid in grids)
            {
                var collidingBox = _transform.GetWorldMatrix(grid).TransformBox(Comp<MapGridComponent>(grid).LocalAABB);

                if (positiveX == true)
                {
                    var newLeft = Math.Max(targetAABB.Left, collidingBox.Right + _random.NextFloat(minMargin, maxMargin));
                    targetAABB.Right = newLeft + targetAABB.Width;
                    targetAABB.Left = newLeft;
                }
                else if (positiveX == false)
                {
                    var newRight = Math.Min(targetAABB.Right, collidingBox.Left - _random.NextFloat(minMargin, maxMargin));
                    targetAABB.Left = newRight - targetAABB.Width;
                    targetAABB.Right = newRight;
                }
                else
                {
                    var margin = _random.NextFloat(-maxMargin, maxMargin);
                    targetAABB.Left += margin;
                    targetAABB.Right += margin;
                }

                if (positiveY == true)
                {
                    var newBottom = Math.Max(targetAABB.Bottom, collidingBox.Top + _random.NextFloat(minMargin, maxMargin));
                    targetAABB.Top = newBottom + targetAABB.Height;
                    targetAABB.Bottom = newBottom;
                }
                else if (positiveY == false)
                {
                    var newTop = Math.Min(targetAABB.Top, collidingBox.Bottom - _random.NextFloat(minMargin, maxMargin));
                    targetAABB.Bottom = newTop - targetAABB.Height;
                    targetAABB.Top = newTop;
                }
                else
                {
                    var margin = _random.NextFloat(-maxMargin, maxMargin);
                    targetAABB.Bottom += margin;
                    targetAABB.Top += margin;
                }
            }
            iteration++;
        }
        // End Frontier

        // Now we have a targetAABB. This has already been expanded to account for our fat ass.
        Vector2 spawnPos;

        if (TryComp<PhysicsComponent>(shuttleUid, out var shuttleBody))
        {
            _physics.SetLinearVelocity(shuttleUid, Vector2.Zero, body: shuttleBody);
            _physics.SetAngularVelocity(shuttleUid, 0f, body: shuttleBody);
        }

        // Frontier: spawn in our AABB
        // TODO: This should prefer the position's angle instead.
        // TODO: This is pretty crude for multiple landings.
        spawnPos = targetAABB.Center;
        // End Frontier

        var offset = Vector2.Zero;
        MapGridComponent? shuttleGrid = null;

        // Offset it because transform does not correspond to AABB position.
        if (TryComp(shuttleUid, out shuttleGrid))
        {
            offset = -shuttleGrid.LocalAABB.Center;
        }

        if (!HasComp<MapComponent>(targetXform.GridUid))
        {
            angle = _random.NextAngle();
        }
        else
        {
            angle = Angle.Zero;
        }

        if (shuttleGrid != null)
        {
            var mapId = targetXform.MapID;
            const int maxResolveIterations = 6;
            const float extraMargin = 2f;
            for (var i = 0; i < maxResolveIterations; i++)
            {
                var aabb = new Box2Rotated(shuttleGrid.LocalAABB, angle) .CalcBoundingBox() .Enlarged(extraMargin) .Translated(spawnPos);
                var intersecting = new List<Entity<MapGridComponent>>();
                _mapManager.FindGridsIntersecting(mapId, aabb, ref intersecting);
                intersecting.RemoveAll(e => e.Owner == shuttleUid || e.Owner == targetXform.GridUid);
                if (intersecting.Count == 0) break;
                var otherGridUid = intersecting[0].Owner;
                if (!_xformQuery.TryGetComponent(otherGridUid, out var otherXform)) break;
                var otherPos = _transform.GetWorldPosition(otherXform);
                var dir = spawnPos - otherPos;
                if (dir == Vector2.Zero) dir = _random.NextAngle().ToVec();
                if (dir != Vector2.Zero) dir = Vector2.Normalize(dir);
                var moveDist = MathF.Max(shuttleGrid.LocalAABB.Width, shuttleGrid.LocalAABB.Height) * 0.5f + maxMargin;
                spawnPos += dir * moveDist;
            }
        }
        var transform = new Transform(spawnPos, angle);
        var adjustedOffset = Robust.Shared.Physics.Transform.Mul(transform, offset);

        coordinates = new EntityCoordinates(targetXform.MapUid.Value, adjustedOffset);
        return true;
    }

    /// <summary>
    /// Tries to arrive nearby without overlapping with other grids.
    /// </summary>
    public bool TryFTLProximity(EntityUid shuttleUid, EntityUid targetUid, TransformComponent? xform = null, TransformComponent? targetXform = null)
    {
        if (!Resolve(targetUid, ref targetXform) ||
            targetXform.MapUid == null ||
            !targetXform.MapUid.Value.IsValid() ||
            !Resolve(shuttleUid, ref xform))
        {
            return false;
        }

        if (!TryGetFTLProximity(shuttleUid, new EntityCoordinates(targetUid, Vector2.Zero), out var coords, out var angle, xform: xform, targetXform: targetXform))
            return false;

        _transform.SetCoordinates(shuttleUid, xform, coords, rotation: angle);
        return true;
    }

    /// <summary>
    /// Tries to FTL to the target coordinates; will move nearby if not possible.
    /// </summary>
    public bool TryFTLProximity(Entity<TransformComponent?> shuttle, EntityCoordinates targetCoordinates)
    {
        if (!Resolve(shuttle.Owner, ref shuttle.Comp) ||
            _transform.GetMap(targetCoordinates)?.IsValid() != true)
        {
            return false;
        }

        if (!TryGetFTLProximity(shuttle, targetCoordinates, out var coords, out var angle))
            return false;

        _transform.SetCoordinates(shuttle, shuttle.Comp, coords, rotation: angle);
        return true;
    }

    /// <summary>
    /// Flattens / deletes everything under the grid upon FTL.
    /// </summary>
    private void Smimsh(EntityUid uid, FixturesComponent? manager = null, MapGridComponent? grid = null, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref manager, ref grid, ref xform) || xform.MapUid == null)
            return;

        if (!TryComp(xform.MapUid, out BroadphaseComponent? lookup))
            return;

        // Flatten anything not parented to a grid.
        var transform = _physics.GetRelativePhysicsTransform((uid, xform), xform.MapUid.Value);
        var aabbs = new List<Box2>(manager.Fixtures.Count);
        var tileSet = new List<(Vector2i, Tile)>();

        foreach (var fixture in manager.Fixtures.Values)
        {
            if (xform.MapID == _ticker.DefaultMap)
                break; //Frontier - FTL is too buggy to let it just fucking gib people wtf - so we disable for frontier's z-level

            if (!fixture.Hard)
                continue;

            var aabb = fixture.Shape.ComputeAABB(transform, 0);

            // Shift it slightly
            // Create a small border around it.
            aabb = aabb.Enlarged(0.2f);
            aabbs.Add(aabb);

            // Handle clearing biome stuff as relevant.
            tileSet.Clear();
            _biomes.ReserveTiles(xform.MapUid.Value, aabb, tileSet);
            _lookupEnts.Clear();
            _immuneEnts.Clear();
            // TODO: Ideally we'd query first BEFORE moving grid but needs adjustments above.
            _lookup.GetLocalEntitiesIntersecting(xform.MapUid.Value, fixture.Shape, transform, _lookupEnts, flags: LookupFlags.Uncontained, lookup: lookup);

            foreach (var ent in _lookupEnts)
            {
                if (ent == uid || _immuneEnts.Contains(ent))
                {
                    continue;
                }

                // If it's on our grid ignore it.
                if (!_xformQuery.TryComp(ent, out var childXform) || childXform.GridUid == uid)
                {
                    continue;
                }

                // If it has the FTLSmashImmuneComponent ignore it.
                if (_immuneQuery.HasComponent(ent))
                {
                    continue;
                }

                if (_bodyQuery.TryGetComponent(ent, out var mob))
                {
                    _logger.Add(LogType.Gib, LogImpact.Extreme, $"{ToPrettyString(ent):player} got gibbed by the shuttle" +
                                                                $" {ToPrettyString(uid)} arriving from FTL at {xform.Coordinates:coordinates}");
                    var gibs = _bobby.GibBody(ent, body: mob);
                    _immuneEnts.UnionWith(gibs);
                    continue;
                }

                QueueDel(ent);
            }
        }

        var ev = new ShuttleFlattenEvent(xform.MapUid.Value, aabbs);
        RaiseLocalEvent(ref ev);
    }
}
