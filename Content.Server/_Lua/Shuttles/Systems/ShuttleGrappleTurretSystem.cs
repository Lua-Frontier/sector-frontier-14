// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Mono.Radar;
using Content.Server.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Spawners;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Server._Lua.Shuttles.Systems;

public sealed class ShuttleGrappleTurretSystem : EntitySystem
{
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly ShuttleGridAccessSystem _gridAccess = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleGrappleTurretComponent, GunShotEvent>(OnTurretShot);
        SubscribeLocalEvent<ShuttleGrappleTurretComponent, ComponentShutdown>(OnTurretShutdown);

        SubscribeLocalEvent<ShuttleGrapplingHookProjectileComponent, ProjectileEmbedEvent>(OnHookEmbedded);
        SubscribeLocalEvent<ShuttleGrapplingHookProjectileComponent, EntityTerminatingEvent>(OnHookTerminating);

        // Directed FTLComponent/IShuttleGrid FTL slots are already owned elsewhere.
        SubscribeLocalEvent<FTLComponentStartupEvent>(OnFtlComponentStartup);
        SubscribeLocalEvent<FTLStartedEvent>(OnFtlStarted);
    }

    private void OnFtlComponentStartup(ref FTLComponentStartupEvent args)
    {
        ReleaseGrapplesInvolving(args.Entity);
    }

    private void OnFtlStarted(ref FTLStartedEvent args)
    {
        ReleaseGrapplesInvolving(args.Entity);
    }

    /// <summary>
    /// Clears every grapple tether owned by or attached to this grid.
    /// </summary>
    private void ReleaseGrapplesInvolving(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<ShuttleGrappleTurretComponent, TransformComponent>();
        while (query.MoveNext(out var turretUid, out var turret, out var xform))
        {
            var onThisGrid = xform.GridUid == gridUid;
            var tetheredHere = turret.OwnerGrid == gridUid || turret.TargetGrid == gridUid;
            if (!onThisGrid && !tetheredHere)
                continue;

            if (turret.HookProjectile == null && turret.JointId == null)
                continue;

            ClearTether(turretUid, turret);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShuttleGrappleTurretComponent, TransformComponent>();
        while (query.MoveNext(out var turretUid, out var turret, out _))
        {
            if (turret.HookProjectile is { } hook)
            {
                if (TerminatingOrDeleted(hook) || !Exists(hook))
                {
                    ClearTetherState(turretUid, turret, deleteHook: false);
                    continue;
                }

                UpdateRadarRope(turretUid, hook);
            }
            else if (turret.JointId != null)
            {
                ClearTetherState(turretUid, turret, deleteHook: false);
                continue;
            }
            else
            {
                continue;
            }

            if (turret.JointId == null ||
                turret.OwnerGrid is not { } ownerGrid ||
                turret.TargetGrid is not { } targetGrid ||
                TerminatingOrDeleted(ownerGrid) ||
                TerminatingOrDeleted(targetGrid))
            {
                continue;
            }

            if (!TryComp<JointComponent>(ownerGrid, out var jointComp) ||
                !jointComp.GetJoints.TryGetValue(turret.JointId, out var joint) ||
                joint is not DistanceJoint distance)
            {
                continue;
            }

            var hookUid = turret.HookProjectile!.Value;
            var currentDistance = (_xform.GetWorldPosition(hookUid) - _xform.GetWorldPosition(turretUid)).Length();
            var newMax = MathF.Min(distance.MaxLength, currentDistance + 0.05f);
            if (newMax >= distance.MaxLength - 0.001f)
                continue;

            distance.MaxLength = newMax;
            distance.Length = MathF.Min(distance.Length, distance.MaxLength);
            Dirty(ownerGrid, jointComp);

            if (TryComp<PhysicsComponent>(ownerGrid, out var ownerPhys))
                _physics.WakeBody(ownerGrid, body: ownerPhys);
            if (TryComp<PhysicsComponent>(targetGrid, out var targetPhys))
                _physics.WakeBody(targetGrid, body: targetPhys);
        }
    }

    private void OnTurretShot(EntityUid uid, ShuttleGrappleTurretComponent component, ref GunShotEvent args)
    {
        // FireControl + SemiAuto leaves ShotCounter stuck at 1; always clear it for re-fire.
        if (TryComp<GunComponent>(uid, out var gun))
        {
            gun.ShotCounter = 0;
            DirtyField(uid, gun, nameof(GunComponent.ShotCounter));
        }

        // Toggle: hook already out → this shot only retracts.
        if (IsHookOut(component))
        {
            ClearTether(uid, component);
            foreach (var (shotUid, _) in args.Ammo)
            {
                if (shotUid is { } proj)
                    QueueDel(proj);
            }

            return;
        }

        foreach (var (shotUid, _) in args.Ammo)
        {
            if (shotUid is not { } proj)
                continue;

            if (!TryComp<ShuttleGrapplingHookProjectileComponent>(proj, out var hook))
                continue;

            if (TryComp<ProjectileComponent>(proj, out var projectile))
            {
                projectile.Weapon = uid;
                Dirty(proj, projectile);
            }

            component.HookProjectile = proj;
            hook.Weapon = uid;

            var visuals = EnsureComp<JointVisualsComponent>(proj);
            visuals.Sprite = new SpriteSpecifier.Rsi(
                new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"),
                "rope");
            visuals.OffsetA = new Vector2(0f, 0.5f);
            visuals.OffsetB = Vector2.Zero;
            visuals.Target = GetNetEntity(uid);
            Dirty(proj, visuals);

            UpdateRadarRope(uid, proj);
            Dirty(uid, component);
        }
    }

    private void OnTurretShutdown(EntityUid uid, ShuttleGrappleTurretComponent component, ComponentShutdown args)
    {
        ClearTether(uid, component);
    }

    private void OnHookEmbedded(EntityUid uid, ShuttleGrapplingHookProjectileComponent component, ref ProjectileEmbedEvent args)
    {
        var weapon = args.Weapon != EntityUid.Invalid
            ? args.Weapon
            : component.Weapon ?? EntityUid.Invalid;

        if (weapon == EntityUid.Invalid || !TryComp<ShuttleGrappleTurretComponent>(weapon, out var turret))
            return;

        component.Weapon = weapon;

        var weaponXform = Transform(weapon);
        if (weaponXform.GridUid is not { } ownerGrid)
            return;

        var embeddedXform = Transform(args.Embedded);
        if (embeddedXform.GridUid is not { } targetGrid)
            return;

        if (ownerGrid == targetGrid)
            return;

        if (!_gridAccess.HasAnyGridType(ownerGrid) || !_gridAccess.HasAnyGridType(targetGrid))
            return;

        var ownerGridXform = Transform(ownerGrid);
        var targetGridXform = Transform(targetGrid);
        if (ownerGridXform.MapID != targetGridXform.MapID)
            return;

        // Pin only after a real shuttle tether is created.
        if (HasComp<TimedDespawnComponent>(uid))
            RemComp<TimedDespawnComponent>(uid);

        var worldHookPos = _xform.GetWorldPosition(uid);
        var worldTurretPos = _xform.GetWorldPosition(weapon);

        var ownerLocal = _xform.ToCoordinates((ownerGrid, ownerGridXform), new MapCoordinates(worldTurretPos, ownerGridXform.MapID)).Position;
        var targetLocal = _xform.ToCoordinates((targetGrid, targetGridXform), new MapCoordinates(worldHookPos, targetGridXform.MapID)).Position;

        var jointId = $"shuttle-grapple-{GetNetEntity(weapon)}";
        _joints.RemoveJoint(ownerGrid, jointId);

        var joint = _joints.CreateDistanceJoint(ownerGrid, targetGrid, ownerLocal, targetLocal, id: jointId);
        joint.CollideConnected = false;
        joint.MaxLength = joint.Length + 0.05f;
        joint.MinLength = 0f;
        joint.Stiffness = 0f;

        turret.JointId = jointId;
        turret.OwnerGrid = ownerGrid;
        turret.TargetGrid = targetGrid;
        turret.HookProjectile = uid;
        Dirty(weapon, turret);

        component.JointId = jointId;
        component.OwnerGrid = ownerGrid;
        component.TargetGrid = targetGrid;

        UpdateRadarRope(weapon, uid);
    }

    private void OnHookTerminating(EntityUid uid, ShuttleGrapplingHookProjectileComponent component, ref EntityTerminatingEvent args)
    {
        if (component.OwnerGrid is { } ownerGrid && !string.IsNullOrEmpty(component.JointId))
            _joints.RemoveJoint(ownerGrid, component.JointId!);

        if (component.Weapon is not { } weapon ||
            !TryComp<ShuttleGrappleTurretComponent>(weapon, out var turret))
            return;

        if (turret.HookProjectile != null && turret.HookProjectile != uid)
            return;

        ClearTetherState(weapon, turret, deleteHook: false);
    }

    private bool IsHookOut(ShuttleGrappleTurretComponent component)
    {
        if (component.JointId != null)
            return true;

        return component.HookProjectile is { } hook && Exists(hook) && !TerminatingOrDeleted(hook);
    }

    private void UpdateRadarRope(EntityUid turretUid, EntityUid hookUid)
    {
        if (!TryComp<HitscanRadarComponent>(hookUid, out var radar))
            return;

        radar.Enabled = true;
        radar.StartPosition = _xform.GetWorldPosition(turretUid);
        radar.EndPosition = _xform.GetWorldPosition(hookUid);
        radar.OriginGrid = Transform(turretUid).GridUid;
        radar.RadarColor = Color.FromHex("#53ff6a");
        radar.LineThickness = 2f;
    }

    private void ClearTether(EntityUid turretUid, ShuttleGrappleTurretComponent component)
    {
        ClearTetherState(turretUid, component, deleteHook: true);
    }

    private void ClearTetherState(EntityUid turretUid, ShuttleGrappleTurretComponent component, bool deleteHook)
    {
        if (component.OwnerGrid is { } ownerGrid && !string.IsNullOrEmpty(component.JointId))
            _joints.RemoveJoint(ownerGrid, component.JointId!);

        if (deleteHook &&
            component.HookProjectile is { } hook &&
            Exists(hook) &&
            !TerminatingOrDeleted(hook))
        {
            QueueDel(hook);
        }

        component.JointId = null;
        component.OwnerGrid = null;
        component.TargetGrid = null;
        component.HookProjectile = null;
        Dirty(turretUid, component);
    }
}
