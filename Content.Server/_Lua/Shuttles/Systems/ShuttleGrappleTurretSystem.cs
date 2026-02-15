// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Shuttles.Components;
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
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleGrappleTurretComponent, GunShotEvent>(OnTurretShot);
        SubscribeLocalEvent<ShuttleGrappleTurretComponent, ComponentShutdown>(OnTurretShutdown);

        SubscribeLocalEvent<ShuttleGrapplingHookProjectileComponent, ProjectileEmbedEvent>(OnHookEmbedded);
        SubscribeLocalEvent<ShuttleGrapplingHookProjectileComponent, EntityTerminatingEvent>(OnHookTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShuttleGrappleTurretComponent, TransformComponent>();
        while (query.MoveNext(out var turretUid, out var turret, out var turretXform))
        {
            if (turret.JointId == null ||
                turret.OwnerGrid is not { } ownerGrid ||
                turret.TargetGrid is not { } targetGrid ||
                turret.HookProjectile is not { } hook ||
                TerminatingOrDeleted(ownerGrid) ||
                TerminatingOrDeleted(targetGrid) ||
                TerminatingOrDeleted(hook))
            {
                continue;
            }

            if (!TryComp<JointComponent>(ownerGrid, out var jointComp) ||
                !jointComp.GetJoints.TryGetValue(turret.JointId, out var joint) ||
                joint is not DistanceJoint distance)
            {
                continue;
            }

            var turretWorld = _xform.GetWorldPosition(turretUid);
            var hookWorld = _xform.GetWorldPosition(hook);
            var currentDistance = (hookWorld - turretWorld).Length();

            var newMax = MathF.Min(distance.MaxLength, currentDistance + 0.05f);
            if (newMax < distance.MaxLength - 0.001f)
            {
                distance.MaxLength = newMax;
                distance.Length = MathF.Min(distance.Length, distance.MaxLength);
                Dirty(ownerGrid, jointComp);

                if (TryComp<PhysicsComponent>(ownerGrid, out var ownerPhys))
                    _physics.WakeBody(ownerGrid, body: ownerPhys);
                if (TryComp<PhysicsComponent>(targetGrid, out var targetPhys))
                    _physics.WakeBody(targetGrid, body: targetPhys);
            }
        }
    }

    private void OnTurretShot(EntityUid uid, ShuttleGrappleTurretComponent component, ref GunShotEvent args)
    {
        ClearTether(uid, component);

        foreach (var (shotUid, _) in args.Ammo)
        {
            if (shotUid is not { } proj)
                continue;

            if (!TryComp<ShuttleGrapplingHookProjectileComponent>(proj, out var hook))
                continue;

            component.HookProjectile = proj;
            hook.Weapon = uid;
            var visuals = EnsureComp<JointVisualsComponent>(proj);
            visuals.Sprite = new SpriteSpecifier.Rsi(
                new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"),
                "rope");
            visuals.OffsetA = new Vector2(0f, 0.5f);
            visuals.OffsetB = Vector2.Zero;
            var wXform = Transform(uid);
            if (wXform.GridUid is { } gridUid)
            {
                visuals.Target = GetNetEntity(gridUid);
                visuals.OffsetB = wXform.LocalPosition;
            }
            else
            {
                visuals.Target = GetNetEntity(uid);
                visuals.OffsetB = Vector2.Zero;
            }

            Dirty(proj, visuals);
        }
    }

    private void OnTurretShutdown(EntityUid uid, ShuttleGrappleTurretComponent component, ComponentShutdown args)
    {
        ClearTether(uid, component);
    }

    private void OnHookEmbedded(EntityUid uid, ShuttleGrapplingHookProjectileComponent component, ref ProjectileEmbedEvent args)
    {
        if (!TryComp<ShuttleGrappleTurretComponent>(args.Weapon, out var turret))
            return;
        if (HasComp<TimedDespawnComponent>(uid))
            RemComp<TimedDespawnComponent>(uid);

        var weaponXform = Transform(args.Weapon);
        if (weaponXform.GridUid is not { } ownerGrid)
            return;

        var embeddedXform = Transform(args.Embedded);
        if (embeddedXform.GridUid is not { } targetGrid)
            return;

        if (ownerGrid == targetGrid)
            return;

        if (!HasComp<ShuttleComponent>(ownerGrid) || !HasComp<ShuttleComponent>(targetGrid))
            return;
        var ownerGridXform = Transform(ownerGrid);
        var targetGridXform = Transform(targetGrid);

        if (ownerGridXform.MapID != targetGridXform.MapID)
            return;

        var worldHookPos = _xform.GetWorldPosition(uid);
        var worldTurretPos = _xform.GetWorldPosition(args.Weapon);

        var ownerLocal = _xform.ToCoordinates((ownerGrid, ownerGridXform), new MapCoordinates(worldTurretPos, ownerGridXform.MapID)).Position;
        var targetLocal = _xform.ToCoordinates((targetGrid, targetGridXform), new MapCoordinates(worldHookPos, targetGridXform.MapID)).Position;

        var jointId = $"shuttle-grapple-{GetNetEntity(args.Weapon)}";
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
        Dirty(args.Weapon, turret);

        component.Weapon = args.Weapon;
        component.JointId = jointId;
        component.OwnerGrid = ownerGrid;
        component.TargetGrid = targetGrid;
    }

    private void OnHookTerminating(EntityUid uid, ShuttleGrapplingHookProjectileComponent component, ref EntityTerminatingEvent args)
    {
        if (component.OwnerGrid is not { } ownerGrid || string.IsNullOrEmpty(component.JointId))
            return;

        _joints.RemoveJoint(ownerGrid, component.JointId!);
    }

    private void ClearTether(EntityUid turretUid, ShuttleGrappleTurretComponent component)
    {
        if (component.OwnerGrid is { } ownerGrid && !string.IsNullOrEmpty(component.JointId))
        {
            _joints.RemoveJoint(ownerGrid, component.JointId!);
        }

        if (component.HookProjectile is { } hook && Exists(hook))
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

