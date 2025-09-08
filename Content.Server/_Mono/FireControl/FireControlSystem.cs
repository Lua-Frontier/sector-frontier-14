// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 RikuTheKiller
// SPDX-FileCopyrightText: 2025 ScyronX
// SPDX-FileCopyrightText: 2025 ark1368
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// Copyright Rane (elijahrane@gmail.com) 2025
// All rights reserved. Relicensed under AGPL with permission

using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Mono.FireControl;
using Content.Shared.Power;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components; //Lua
using System.Linq;
using Content.Shared.Physics;
using System.Numerics;
using Content.Server.Power.EntitySystems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Timing;
using Content.Shared.Interaction;
using Content.Shared._Mono.ShipGuns;
using Content.Shared.Examine;
using Content.Shared.UserInterface;

namespace Content.Server._Mono.FireControl;

public sealed partial class FireControlSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly RotateToFaceSystem _rotateToFace = default!;

    /// <summary>
    /// Dictionary of entities that have visualization enabled
    /// </summary>
    private readonly HashSet<EntityUid> _visualizedEntities = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FireControlServerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<FireControlServerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FireControlServerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FireControlServerComponent, EntityTerminatingEvent>(OnServerTerminating);

        SubscribeLocalEvent<FireControllableComponent, PowerChangedEvent>(OnControllablePowerChanged);
        SubscribeLocalEvent<FireControllableComponent, ComponentShutdown>(OnControllableShutdown);
        SubscribeLocalEvent<FireControllableComponent, EntParentChangedMessage>(OnControllableParentChanged);

        // Subscribe to grid split events to ensure we update when grids change
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);

        InitializeConsole();
        InitializeTargetGuided();
    }

    private void OnPowerChanged(EntityUid uid, FireControlServerComponent component, PowerChangedEvent args)
    {
        if (args.Powered)
            TryConnect(uid, component);
        else
            Disconnect(uid, component);
    }

    private void OnShutdown(EntityUid uid, FireControlServerComponent component, ComponentShutdown args)
    {
        Disconnect(uid, component);
    }

    private void OnServerTerminating(EntityUid uid, FireControlServerComponent component, ref EntityTerminatingEvent args)
    {
        Disconnect(uid, component);
    }

    private void OnExamined(EntityUid uid, FireControlServerComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        args.PushMarkup(
            Loc.GetString(
                "gunnery-server-examine-detail",
                ("usedProcessingPower", component.UsedProcessingPower),
                ("processingPower", component.ProcessingPower),
                ("valueColor", component.UsedProcessingPower <= component.ProcessingPower - 2 ? "green" : "yellow")
            )
        );
    }

    private void OnControllablePowerChanged(EntityUid uid, FireControllableComponent component, PowerChangedEvent args)
    {
        if (args.Powered)
            TryRegister(uid, component);
        else
            Unregister(uid, component);
    }

    private void OnControllableShutdown(EntityUid uid, FireControllableComponent component, ComponentShutdown args)
    {
        if (component.ControllingServer != null && TryComp<FireControlServerComponent>(component.ControllingServer, out var server))
        {
            Unregister(uid, component);

            foreach (var console in server.Consoles)
            {
                if (TryComp<FireControlConsoleComponent>(console, out var consoleComp))
                {
                    UpdateUi(console, consoleComp);
                }
            }
        }
    }

    private void OnControllableParentChanged(EntityUid uid, FireControllableComponent component, ref EntParentChangedMessage args)
    {
        if (component.ControllingServer == null)
            return;

        // Check if the weapon is still on the same grid as its controlling server
        if (!TryComp<FireControlServerComponent>(component.ControllingServer, out var server) ||
            server.ConnectedGrid == null)
            return;

        var currentGrid = _xform.GetGrid(uid);
        if (currentGrid != server.ConnectedGrid)
        {
            // Weapon is no longer on the same grid - unregister it
            Unregister(uid, component);

            // Update UI for any connected consoles
            foreach (var console in server.Consoles)
            {
                if (TryComp<FireControlConsoleComponent>(console, out var consoleComp))
                {
                    UpdateUi(console, consoleComp);
                }
            }
        }
    }

    private void Disconnect(EntityUid server, FireControlServerComponent? component = null)
    {
        if (!Resolve(server, ref component))
            return;

        // Clean up grid connection if it exists
        if (component.ConnectedGrid != null && Exists(component.ConnectedGrid) && TryComp<FireControlGridComponent>(component.ConnectedGrid, out var controlGrid))
        {
            if (controlGrid.ControllingServer == server)
            {
                controlGrid.ControllingServer = null;
                RemComp<FireControlGridComponent>((EntityUid)component.ConnectedGrid);
            }
        }

        // Unregister all controlled entities
        var controlledCopy = component.Controlled.ToList(); // Create copy to avoid modification during iteration
        foreach (var controllable in controlledCopy)
        {
            if (Exists(controllable))
                Unregister(controllable);
        }

        // Unregister all consoles
        var consolesCopy = component.Consoles.ToList(); // Create copy to avoid modification during iteration
        foreach (var console in consolesCopy)
        {
            if (Exists(console))
                UnregisterConsole(console);
        }

        // Clear the server's state
        component.Controlled.Clear();
        component.Consoles.Clear();
        component.ConnectedGrid = null;
        component.UsedProcessingPower = 0;
    }

    public void RefreshControllables(EntityUid grid, FireControlGridComponent? component = null)
    {
        if (!Resolve(grid, ref component))
            return;

        if (component.ControllingServer == null)
            return;

        // Check if the controlling server still exists
        if (!Exists(component.ControllingServer) || !TryComp<FireControlServerComponent>(component.ControllingServer, out var server))
        {
            // Clear the invalid reference
            component.ControllingServer = null;
            return;
        }

        server.Controlled.Clear();
        server.UsedProcessingPower = 0;

        var query = EntityQueryEnumerator<FireControllableComponent>();

        while (query.MoveNext(out var controllable, out var controlComp))
        {
            if (_xform.GetGrid(controllable) == grid)
                TryRegister(controllable, controlComp);
        }

        foreach (var console in server.Consoles)
            UpdateUi(console);
    }

    private bool TryConnect(EntityUid server, FireControlServerComponent? component = null)
    {
        if (!Resolve(server, ref component))
            return false;

        var grid = _xform.GetGrid(server);

        if (grid == null)
            return false;

        var controlGrid = EnsureComp<FireControlGridComponent>((EntityUid)grid);

        // Check if there's already a controlling server and if it's valid
        if (controlGrid.ControllingServer != null)
        {
            // If the controlling server no longer exists, clear the reference
            if (!Exists(controlGrid.ControllingServer) || !TryComp<FireControlServerComponent>(controlGrid.ControllingServer, out _))
            {
                controlGrid.ControllingServer = null;
            }
            else
            {
                // Valid server already exists, cannot connect
                return false;
            }
        }

        controlGrid.ControllingServer = server;
        component.ConnectedGrid = grid;

        RefreshControllables((EntityUid)grid, controlGrid);

        return true;
    }

    private void Unregister(EntityUid controllable, FireControllableComponent? component = null)
    {
        if (!Resolve(controllable, ref component))
            return;

        if (component.ControllingServer == null || !TryComp<FireControlServerComponent>(component.ControllingServer, out var controlComp))
            return;

        controlComp.Controlled.Remove(controllable);
        controlComp.UsedProcessingPower -= GetProcessingPowerCost(controllable, component);
        component.ControllingServer = null;
    }

    private bool TryRegister(EntityUid controllable, FireControllableComponent? component = null)
    {
        if (!Resolve(controllable, ref component))
            return false;

        var gridServer = TryGetGridServer(controllable);

        if (gridServer.ServerUid == null || gridServer.ServerComponent == null)
            return false;

        var processingPowerCost = GetProcessingPowerCost(controllable, component);

        if (processingPowerCost > GetRemainingProcessingPower(gridServer.ServerUid.Value, gridServer.ServerComponent))
            return false;

        if (gridServer.ServerComponent.Controlled.Add(controllable))
        {
            gridServer.ServerComponent.UsedProcessingPower += processingPowerCost;
            component.ControllingServer = gridServer.ServerUid;
            return true;
        }
        else
        {
            return false;
        }
    }

    public int GetRemainingProcessingPower(EntityUid server, FireControlServerComponent? component = null)
    {
        if (!Resolve(server, ref component))
            return 0;

        return component.ProcessingPower - component.UsedProcessingPower;
    }

    public int GetProcessingPowerCost(EntityUid controllable, FireControllableComponent? component = null)
    {
        if (!Resolve(controllable, ref component))
            return 0;

        if (!TryComp<ShipGunClassComponent>(controllable, out var classComponent))
            return 0;

        return classComponent.Class switch
        {
            ShipGunClass.Superlight => 1,
            ShipGunClass.Light => 3,
            ShipGunClass.Medium => 6,
            ShipGunClass.Heavy => 9,
            ShipGunClass.Superheavy => 12,
            _ => 0,
        };
    }

    private (EntityUid? ServerUid, FireControlServerComponent? ServerComponent) TryGetGridServer(EntityUid uid)
    {
        var grid = _xform.GetGrid(uid);

        if (grid == null)
            return (null, null);

        if (!TryComp<FireControlGridComponent>(grid, out var controlGrid))
            return (null, null);

        if (controlGrid.ControllingServer == null)
            return (null, null);

        // Check if the controlling server still exists and has the component
        if (!Exists(controlGrid.ControllingServer) || !TryComp<FireControlServerComponent>(controlGrid.ControllingServer, out var server))
        {
            // Clear the invalid reference
            controlGrid.ControllingServer = null;
            return (null, null);
        }

        return (controlGrid.ControllingServer, server);
    }

    /// <summary>
    /// Cleans up all invalid server references across all grids
    /// </summary>
    public void CleanupInvalidServerReferences()
    {
        var gridQuery = EntityQueryEnumerator<FireControlGridComponent>();

        while (gridQuery.MoveNext(out var gridUid, out var gridComponent))
        {
            if (gridComponent.ControllingServer != null)
            {
                if (!Exists(gridComponent.ControllingServer) || !TryComp<FireControlServerComponent>(gridComponent.ControllingServer, out _))
                {
                    gridComponent.ControllingServer = null;
                    RemComp<FireControlGridComponent>(gridUid);
                }
            }
        }
    }

    /// <summary>
    /// Forces all powered servers on a specific grid to attempt reconnection
    /// </summary>
    public void ForceServerReconnectionOnGrid(EntityUid gridUid)
    {
        var serverQuery = EntityQueryEnumerator<FireControlServerComponent>();

        while (serverQuery.MoveNext(out var serverUid, out var serverComponent))
        {
            var serverGrid = _xform.GetGrid(serverUid);
            if (serverGrid == gridUid && _power.IsPowered(serverUid))
            {
                // Force reconnection attempt
                TryConnect(serverUid, serverComponent);
            }
        }
    }

    public void FireWeapons(EntityUid server, List<NetEntity> weapons, NetCoordinates coordinates, FireControlServerComponent? component = null)
    {
        if (!Resolve(server, ref component))
            return;

        // Check if the weapon's grid is in FTL
        var grid = component.ConnectedGrid;
        if (grid != null && TryComp<FTLComponent>((EntityUid)grid, out var ftlComp))
        {
            // Cannot fire weapons during FTL travel
            return;
        }

        var targetCoords = GetCoordinates(coordinates);

        foreach (var weapon in weapons)
        {
            var localWeapon = GetEntity(weapon);
            if (!Exists(localWeapon) || !component.Controlled.Contains(localWeapon))
                continue;

            if (!TryComp<GunComponent>(localWeapon, out var gun))
                continue;

            if (TryComp<TransformComponent>(localWeapon, out var weaponXform))
            {
                var currentMapCoords = _xform.GetMapCoordinates(localWeapon, weaponXform);
                var destinationMapCoords = targetCoords.ToMap(EntityManager, _xform);

                if (destinationMapCoords.MapId == currentMapCoords.MapId && currentMapCoords.MapId != MapId.Nullspace)
                {
                    var diff = destinationMapCoords.Position - currentMapCoords.Position;
                    if (diff.LengthSquared() > 0.01f)
                    {
                        // Only rotate the gun if it has line of sight to the target
                        if (HasLineOfSight(localWeapon, currentMapCoords.Position, destinationMapCoords.Position, currentMapCoords.MapId))
                        {
                            var goalAngle = Angle.FromWorldVec(diff);
                            _rotateToFace.TryRotateTo(localWeapon, goalAngle, 0f, Angle.FromDegrees(1), float.MaxValue, weaponXform);
                        }
                    }
                }
            }

            var weaponX = Transform(localWeapon);
            var targetPos = targetCoords.ToMap(EntityManager, _xform);

            if (targetPos.MapId != weaponX.MapID)
                continue;

            var weaponPos = _xform.GetWorldPosition(weaponX);

            // Get direction to target
            var direction = (targetPos.Position - weaponPos);
            var distance = direction.Length();
            if (distance <= 0)
                continue;

            direction = Vector2.Normalize(direction);

            // Check for obstacles in the firing direction
            if (!CanFireInDirection(localWeapon, weaponPos, direction, targetPos.Position, weaponX.MapID))
                continue;

            // If we can fire, fire the weapon
            _gun.AttemptShoot(localWeapon, localWeapon, gun, targetCoords);
        }
    }
	
	//Lua start
    /// <summary>
    /// Convenience: attempt to aim and fire all controllable weapons on a grid at a world position.
    /// </summary>
    public void TryAimAndFireGrid(EntityUid gridUid, Vector2 worldTarget)
    {
        if (!TryComp<FireControlGridComponent>(gridUid, out var controlGrid) || controlGrid.ControllingServer == null)
            return;

        if (!TryComp<FireControlServerComponent>(controlGrid.ControllingServer.Value, out var server))
            return;

        // Skip during FTL
        if (server.ConnectedGrid != null && TryComp<FTLComponent>((EntityUid)server.ConnectedGrid, out var ftl) &&
            (ftl.State & (Content.Shared.Shuttles.Systems.FTLState.Starting | Content.Shared.Shuttles.Systems.FTLState.Travelling | Content.Shared.Shuttles.Systems.FTLState.Arriving)) != 0x0)
            return;

        var coords = _xform.ToCoordinates(new MapCoordinates(worldTarget, Transform(gridUid).MapID));

        // Precompute grid forward for nose-arc checks
        var gridRot = _xform.GetWorldRotation(gridUid);
        var gridForward = gridRot.RotateVec(Vector2.UnitY);

        foreach (var weapon in server.Controlled.OrderByDescending(w => GetWeaponPriority(w)))
        {
            if (!TryComp<FireControllableComponent>(weapon, out var comp))
                continue;

            // Check firing arcs before attempting
            if (!TargetWithinArcs(weapon, comp, worldTarget, gridForward))
                continue;

            AttemptFire(weapon, weapon, coords, comp);
        }
    }

    /// <summary>
    /// Convenience: attempt to aim and fire with predictive intercept using a target grid's current velocity.
    /// </summary>
    public void TryAimAndFireGrid(EntityUid gridUid, EntityUid targetGridUid, Vector2 suggestedAim)
    {
        if (!TryComp<FireControlGridComponent>(gridUid, out var controlGrid) || controlGrid.ControllingServer == null)
            return;

        if (!TryComp<FireControlServerComponent>(controlGrid.ControllingServer.Value, out var server))
            return;

        // Skip during FTL
        if (server.ConnectedGrid != null && TryComp<FTLComponent>((EntityUid)server.ConnectedGrid, out var ftl) &&
            (ftl.State & (Content.Shared.Shuttles.Systems.FTLState.Starting | Content.Shared.Shuttles.Systems.FTLState.Travelling | Content.Shared.Shuttles.Systems.FTLState.Arriving)) != 0x0)
            return;

        var mapId = Transform(gridUid).MapID;
        var targetPos = suggestedAim;
        Vector2 targetVel = Vector2.Zero;
        if (TryComp<PhysicsComponent>(targetGridUid, out var targetBody))
        {
            targetVel = targetBody.LinearVelocity;
        }

        var now = _timing.CurTime.TotalSeconds;
        var salvoActive = server.UseSalvos && (now % Math.Max(0.1, server.SalvoPeriodSeconds)) <= server.SalvoWindowSeconds;

        foreach (var weapon in server.Controlled)
        {
            if (!TryComp<FireControllableComponent>(weapon, out var comp))
                continue;

            // Check arcs before attempting
            var gridForward = _xform.GetWorldRotation(gridUid).RotateVec(Vector2.UnitY);
            if (!TargetWithinArcs(weapon, comp, targetPos, gridForward))
                continue;

            // Predict intercept point per-weapon
            Vector2 fireAt = targetPos;
            if (TryComp<GunComponent>(weapon, out var gun))
            {
                var weaponXform = Transform(weapon);
                var weaponPos = _xform.GetWorldPosition(weaponXform);
                var projSpeed = gun.ProjectileSpeedModified > 0f ? gun.ProjectileSpeedModified : 20f;
                var intercept = ComputeIntercept(weaponPos, targetPos, targetVel, projSpeed);
                if (intercept != null)
                    fireAt = intercept.Value;
            }

            // If salvos enabled, fire only inside window, with small jitter per weapon
            if (salvoActive)
            {
                // Determine a deterministic jitter per weapon
                var jitter = (Math.Abs((int)weapon.GetHashCode()) % 1000) / 1000.0 * server.SalvoJitterSeconds;
                if ((now % Math.Max(0.1, server.SalvoPeriodSeconds)) < jitter)
                    continue;
            }

            var coords = _xform.ToCoordinates(new MapCoordinates(fireAt, mapId));
            AttemptFire(weapon, weapon, coords, comp);
        }
    }

    /// <summary>
    /// Returns how many controllable weapons on a grid can currently fire at a world point (arc-only check).
    /// </summary>
    public int CountWeaponsAbleToFireAt(EntityUid gridUid, Vector2 worldTarget)
    {
        if (!TryComp<FireControlGridComponent>(gridUid, out var controlGrid) || controlGrid.ControllingServer == null)
            return 0;
        if (!TryComp<FireControlServerComponent>(controlGrid.ControllingServer.Value, out var server))
            return 0;

        var gridForward = _xform.GetWorldRotation(gridUid).RotateVec(Vector2.UnitY);
        var count = 0;
        foreach (var weapon in server.Controlled.OrderByDescending(w => GetWeaponPriority(w)))
        {
            if (!TryComp<FireControllableComponent>(weapon, out var comp))
                continue;
            if (TargetWithinArcs(weapon, comp, worldTarget, gridForward))
                count++;
        }
        return count;
    }

    private int GetWeaponPriority(EntityUid weapon)
    {
        // Prefer heavy guns when counting potential firepower
        if (TryComp<ShipGunClassComponent>(weapon, out var cls))
        {
            return cls.Class switch
            {
                ShipGunClass.Superheavy => 5,
                ShipGunClass.Heavy => 4,
                ShipGunClass.Medium => 3,
                ShipGunClass.Light => 2,
                ShipGunClass.Superlight => 1,
                _ => 0,
            };
        }
        return 0;
    }

    /// <summary>
    /// Computes an intercept MapCoordinates for a projectile of given speed to hit a target moving with targetVel.
    /// Returns null if no suitable solution.
    /// </summary>
    private Vector2? ComputeIntercept(Vector2 shooterPos, Vector2 targetPos, Vector2 targetVel, float projectileSpeed)
    {
        if (projectileSpeed <= 0f)
            return null;

        var toTarget = targetPos - shooterPos;
        var a = Vector2.Dot(targetVel, targetVel) - projectileSpeed * projectileSpeed;
        var b = 2f * Vector2.Dot(toTarget, targetVel);
        var c = Vector2.Dot(toTarget, toTarget);

        float t;
        const float eps = 1e-4f;
        if (MathF.Abs(a) < eps)
        {
            // Linear solution
            if (MathF.Abs(b) < eps)
                return null;
            t = -c / b;
        }
        else
        {
            var disc = b * b - 4f * a * c;
            if (disc < 0f)
                return null;
            var sqrt = MathF.Sqrt(disc);
            var t1 = (-b + sqrt) / (2f * a);
            var t2 = (-b - sqrt) / (2f * a);
            t = MathF.Min(t1, t2);
            if (t < eps)
                t = MathF.Max(t1, t2);
            if (t < eps)
                return null;
        }

        var intercept = targetPos + targetVel * t;
        return intercept;
    }

    /// <summary>
    /// Returns true if the target is within the weapon's own arc and/or the grid nose arc for AI firing.
    /// Weapon forward is taken as its local +Y (same basis as grid forward).
    /// </summary>
    private bool TargetWithinArcs(EntityUid weapon, FireControllableComponent comp, Vector2 worldTarget, Vector2 gridForward)
    {
        var xform = Transform(weapon);
        var weaponPos = _xform.GetWorldPosition(xform);
        var toTarget = worldTarget - weaponPos;
        if (toTarget.LengthSquared() < 0.0001f)
            return false;
        toTarget = Vector2.Normalize(toTarget);

        // Weapon forward (+Y in local)
        var weaponForward = _xform.GetWorldRotation(xform).RotateVec(Vector2.UnitY);

        // Weapon arc check (treat <=0 or >=360 as always-true)
        var weaponCos = Vector2.Dot(weaponForward, toTarget);
        var weaponAngleOk = comp.FireArcDegrees >= 360f || comp.FireArcDegrees <= 0f ||
                             weaponCos >= MathF.Cos(comp.FireArcDegrees * 0.5f * MathF.PI / 180f);

        if (!comp.UseGridNoseArc)
            return weaponAngleOk;

        // Grid nose arc check (treat <=0 or >=360 as always-true)
        var gridCos = Vector2.Dot(gridForward, toTarget);
        var gridAngleOk = comp.GridNoseArcDegrees >= 360f || comp.GridNoseArcDegrees <= 0f ||
                          gridCos >= MathF.Cos(comp.GridNoseArcDegrees * 0.5f * MathF.PI / 180f);

        // Fire if EITHER arc condition is satisfied
        return weaponAngleOk || gridAngleOk;
    }//lua end

    /// <summary>
    /// Checks all controllables on a grid and unregisters any that don't belong.
    /// </summary>
    /// <param name="server">The GCS server entity</param>
    /// <param name="component">The server component</param>
    public void UpdateAllControllables(EntityUid server, FireControlServerComponent? component = null)
    {
        if (!Resolve(server, ref component) || component.ConnectedGrid == null)
            return;

        // Get a copy of the controlled entities list to avoid modification during iteration
        var controlled = component.Controlled.ToList();

        foreach (var controllable in controlled)
        {
            if (TryComp<FireControllableComponent>(controllable, out var controlComp))
            {
                var currentGrid = _xform.GetGrid(controllable);
                if (currentGrid != component.ConnectedGrid)
                {
                    Unregister(controllable, controlComp);
                }
            }
        }

        // Update UI for all consoles
        foreach (var console in component.Consoles)
        {
            if (TryComp<FireControlConsoleComponent>(console, out var consoleComp))
            {
                UpdateUi(console, consoleComp);
            }
        }
    }

    private void OnGridSplit(ref GridSplitEvent ev)
    {
        // Check all GCS servers for affected grids
        var query = EntityQueryEnumerator<FireControlServerComponent>();

        while (query.MoveNext(out var serverUid, out var server))
        {
            if (server.ConnectedGrid == ev.Grid)
            {
                // Grid has been split, check all controllables
                UpdateAllControllables(serverUid, server);
            }
        }
    }

    /// <summary>
    /// Attempts to fire a weapon, handling aiming and firing logic.
    /// </summary>
    public bool AttemptFire(EntityUid weapon, EntityUid user, EntityCoordinates coords, FireControllableComponent? comp = null)
    {
        if (!Resolve(weapon, ref comp))
            return false;

        // Check if the weapon is ready to fire
        if (!CanFire(weapon, comp))
            return false;

        // Get weapon and target positions
        var weaponXform = Transform(weapon);
        var weaponPos = _xform.GetWorldPosition(weaponXform);
        var targetPos = coords.ToMap(EntityManager, _xform).Position;

        // Calculate direction
        var direction = targetPos - weaponPos;
        var distance = direction.Length();
        if (distance <= float.Epsilon)
            return false; // Can't fire at the same position

        direction = Vector2.Normalize(direction);

        // Check for obstacles in the firing direction
        if (!CanFireInDirection(weapon, weaponPos, direction, targetPos, weaponXform.MapID))
            return false;

        // Set the cooldown for next firing
        comp.NextFire = _timing.CurTime + TimeSpan.FromSeconds(comp.FireCooldown);

        // Try to get a gun component and fire the weapon
        if (TryComp<GunComponent>(weapon, out var gun))
        {
            _gun.AttemptShoot(weapon, user, gun, coords);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a weapon is ready to fire.
    /// </summary>
    private bool CanFire(EntityUid weapon, FireControllableComponent comp)
    {
        // Check if weapon is powered
        if (!_power.IsPowered(weapon))
            return false;

        // Check if weapon is connected to a server
        if (comp.ControllingServer == null)
            return false;

        // Check for other conditions like cooldowns if needed
        if (comp.NextFire > _timing.CurTime)
            return false;

        return true;
    }

    /// <summary>
    /// Checks if a weapon has line of sight to a target position
    /// </summary>
    /// <param name="weapon">The weapon entity</param>
    /// <param name="weaponPos">The weapon's position</param>
    /// <param name="targetPos">The target position</param>
    /// <param name="mapId">The map ID</param>
    /// <param name="maxDistance">Maximum raycast distance in meters</param>
    /// <returns>True if the weapon has line of sight to the target</returns>
    private bool HasLineOfSight(EntityUid weapon, Vector2 weaponPos, Vector2 targetPos, MapId mapId, float maxDistance = 500f)
    {
        // Calculate direction to target
        var direction = (targetPos - weaponPos);
        var distance = direction.Length();
        if (distance <= 0)
            return false; // Can't have LOS to the same position

        direction = Vector2.Normalize(direction);

        // Get the weapon's grid for grid filtering
        var weaponTransform = Transform(weapon);
        var weaponGridUid = weaponTransform.GridUid;

        // Calculate distance to target (capped at maximum distance)
        var targetDistance = Vector2.Distance(weaponPos, targetPos);
        var rayDistance = Math.Min(targetDistance, maxDistance);

        // Initialize ray collision
        var ray = new CollisionRay(weaponPos, direction, collisionMask: (int)(CollisionGroup.Opaque | CollisionGroup.Impassable));

        // Create a predicate that ignores entities not on the same grid
        bool IgnoreEntityNotOnSameGrid(EntityUid entity, EntityUid sourceWeapon)
        {
            // Always ignore the source weapon itself
            if (entity == sourceWeapon)
                return true;

            // If the weapon isn't on a grid, we'll check against all entities
            if (weaponGridUid == null)
                return false;

            // Get the entity's grid
            var entityTransform = Transform(entity);
            var entityGridUid = entityTransform.GridUid;

            // Ignore if not on the same grid
            return entityGridUid != weaponGridUid;
        }

        // Check if there's any obstacles in the line of sight, only considering entities on the same grid
        var raycastResults = _physics.IntersectRayWithPredicate(
            mapId,
            ray,
            weapon,
            IgnoreEntityNotOnSameGrid,
            rayDistance,
            returnOnFirstHit: true // We only need to know if there's ANY obstacle
        ).ToList();

        // Has line of sight if there are no obstacles in the path
        return raycastResults.Count == 0;
    }

    /// <summary>
    /// Checks if a weapon can fire in a specific direction without obstacles
    /// </summary>
    /// <param name="weapon">The weapon entity</param>
    /// <param name="weaponPos">The weapon's position</param>
    /// <param name="direction">Normalized direction vector</param>
    /// <param name="targetPos">The target position</param>
    /// <param name="mapId">The map ID</param>
    /// <param name="maxDistance">Maximum raycast distance in meters</param>
    /// <returns>True if the weapon can fire in that direction</returns>
    private bool CanFireInDirection(EntityUid weapon, Vector2 weaponPos, Vector2 direction, Vector2 targetPos, MapId mapId, float maxDistance = 500f)
    {
        // Use the HasLineOfSight method for consistency
        return HasLineOfSight(weapon, weaponPos, targetPos, mapId, maxDistance);
    }

    /// <summary>
    /// Checks if a weapon can fire in a full 360-degree circle around it to find clear firing lanes
    /// </summary>
    /// <param name="weapon">The weapon entity</param>
    /// <param name="maxDistance">Maximum raycast distance in meters</param>
    /// <param name="rayCount">Number of rays to cast around the entity</param>
    /// <returns>Dictionary mapping directions (angles in degrees) to whether they're clear for firing</returns>
    public Dictionary<float, bool> CheckAllDirections(EntityUid weapon, float maxDistance = 500f, int rayCount = 256)
    {
        var directions = new Dictionary<float, bool>();

        var transform = Transform(weapon);
        var position = _xform.GetWorldPosition(transform);
        var mapId = transform.MapID;
        var weaponGridUid = transform.GridUid;

        // Create a predicate that ignores entities not on the same grid
        bool IgnoreEntityNotOnSameGrid(EntityUid entity, EntityUid sourceWeapon)
        {
            // Always ignore the source weapon itself
            if (entity == sourceWeapon)
                return true;

            // If the weapon isn't on a grid, we'll check against all entities
            if (weaponGridUid == null)
                return false;

            // Get the entity's grid
            var entityTransform = Transform(entity);
            var entityGridUid = entityTransform.GridUid;

            // Ignore if not on the same grid
            return entityGridUid != weaponGridUid;
        }

        // Cast rays in all directions to check for clear firing lanes
        for (var i = 0; i < rayCount; i++)
        {
            // Calculate angle and direction for this ray
            var angle = (i / (float)rayCount) * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            // Initialize ray collision
            var ray = new CollisionRay(position, direction, collisionMask: (int)(CollisionGroup.Opaque | CollisionGroup.Impassable));

            // Check if there's any obstacles in this direction, only considering entities on the same grid
            var raycastResults = _physics.IntersectRayWithPredicate(
                mapId,
                ray,
                weapon,
                IgnoreEntityNotOnSameGrid,
                maxDistance,
                returnOnFirstHit: false
            ).ToList();

            // Direction is clear if there are no obstacles
            var canFire = raycastResults.Count == 0;
            directions[angle * 180 / MathF.PI] = canFire;
        }

        return directions;
    }

    /// <summary>
    /// Sends a visualization event to all clients
    /// </summary>
    /// <param name="entityUid">Entity to visualize</param>
    /// <param name="directions">Firing direction data</param>
    public void SendVisualizationEvent(EntityUid entityUid, Dictionary<float, bool> directions)
    {
        var netEntity = GetNetEntity(entityUid);

        var ev = new FireControlVisualizationEvent(
            netEntity,
            directions
        );

        RaiseNetworkEvent(ev);
    }

    /// <summary>
    /// Toggles visualization for an entity
    /// </summary>
    /// <param name="entityUid">Entity to toggle visualization for</param>
    /// <returns>True if visualization was enabled, false if disabled</returns>
    public bool ToggleVisualization(EntityUid entityUid)
    {
        var netEntity = GetNetEntity(entityUid);

        // Check if already visualized
        if (_visualizedEntities.Contains(entityUid))
        {
            // Turn off visualization
            _visualizedEntities.Remove(entityUid);
            RaiseNetworkEvent(new FireControlVisualizationEvent(netEntity));
            return false;
        }

        // Turn on visualization
        _visualizedEntities.Add(entityUid);
        var directions = CheckAllDirections(entityUid);
        RaiseNetworkEvent(new FireControlVisualizationEvent(netEntity, directions));
        return true;
    }
}

public sealed class FireControllableStatusReportEvent : EntityEventArgs
{
    public List<(string type, string content)> StatusReports = new();
}
