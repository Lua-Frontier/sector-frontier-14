using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._Lua.NPC.Components;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Timing;

namespace Content.Server._Lua.NPC;

public sealed class NpcSmartTurretCombatSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<GunComponent> _gunQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gunQuery = GetEntityQuery<GunComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        UpdatesBefore.Add(typeof(NPCCombatSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NpcSmartTurretComponent, NPCRangedCombatComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var smart, out var ranged, out var xform))
        {
            ApplyCombatSettings(uid, smart, ranged);
            TrackTarget(uid, smart, ranged, xform, frameTime);
        }
    }

    private void ApplyCombatSettings(EntityUid uid, NpcSmartTurretComponent smart, NPCRangedCombatComponent ranged)
    {
        ranged.UseOpaqueForLOSChecks = true;
        ranged.ShootDelay = smart.ShootDelay;
        ranged.AccuracyThreshold = smart.AccuracyThreshold;
        ranged.TargetLeadScale = smart.LeadScale;

        if (_gunQuery.TryGetComponent(uid, out var gun))
        {
            ranged.ProjectileSpeedOverride = gun.ProjectileSpeedModified > 0f
                ? gun.ProjectileSpeedModified
                : gun.ProjectileSpeed;
        }
    }

    private void TrackTarget(
        EntityUid uid,
        NpcSmartTurretComponent smart,
        NPCRangedCombatComponent ranged,
        TransformComponent xform,
        float frameTime)
    {
        if (!_xformQuery.TryGetComponent(ranged.Target, out var targetXform))
            return;

        if (targetXform.MapID != xform.MapID)
            return;

        if (ranged.TargetInLOS)
        {
            smart.LastKnownTargetCoordinates = targetXform.Coordinates;
            smart.LastSeenAt = _timing.CurTime;
            Dirty(uid, smart);
            return;
        }

        var age = (_timing.CurTime - smart.LastSeenAt).TotalSeconds;
        if (age > smart.TrackMemorySeconds || !smart.LastKnownTargetCoordinates.IsValid(EntityManager))
            return;

        var ownPosition = _transform.GetWorldPosition(xform);
        var targetPosition = _transform.ToMapCoordinates(smart.LastKnownTargetCoordinates);
        if (targetPosition.MapId != xform.MapID)
            return;

        var goalRotation = (targetPosition.Position - ownPosition).ToWorldAngle();
        var rotationSpeed = ranged.RotationSpeed?.Theta ?? double.MaxValue;
        _rotate.TryRotateTo(uid, goalRotation, frameTime, ranged.AccuracyThreshold, rotationSpeed, xform);
    }
}
