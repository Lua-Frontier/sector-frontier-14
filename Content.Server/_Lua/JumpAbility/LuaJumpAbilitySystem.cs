// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.JumpAbility;
using Content.Shared._Lua.Sprint;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lua.JumpAbility;

public sealed partial class LuaJumpAbilitySystem : SharedLuaJumpAbilitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    private static readonly ProtoId<StatusEffectPrototype> StunEffect = "Stun";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LuaJumpAbilityComponent, LuaJumpToPointEvent>(OnJumpToPoint);
    }

    protected override void OnDirectionalJump(Entity<LuaJumpAbilityComponent> ent, ref LuaDirectionalJumpEvent args)
    {
        base.OnDirectionalJump(ent, ref args);

        if (!args.Handled)
            return;

        ConsumeSprintForJump(ent.Owner, ent.Comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<LuaJumpAbilityComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsJumping && comp.TimeUntilEndJump > curTime && comp.TimeUntilNextJump <= curTime)
                ApplyJumpImpulse(uid, comp);
        }
    }

    private void OnJumpToPoint(Entity<LuaJumpAbilityComponent> ent, ref LuaJumpToPointEvent args)
    {
        if (args.Handled)
            return;

        if (!HasEnoughSprintForJump(ent.Owner, ent.Comp)) return;
        ent.Comp.IsJumping = true;
        ent.Comp.TimeUntilEndJump = _gameTiming.CurTime + TimeSpan.FromSeconds(ent.Comp.JumpDuration);
        ent.Comp.JumpTarget = args.Target;
        Dirty(ent.Owner, ent.Comp);

        _statusEffect.TryAddStatusEffect<StunnedComponent>(
            ent.Owner, StunEffect, TimeSpan.FromSeconds(ent.Comp.JumpDuration), true);

        if (ent.Comp.JumpSound != null)
            Audio.PlayPvs(ent.Comp.JumpSound, ent.Owner, AudioParams.Default.WithVolume(3));

        ConsumeSprintForJump(ent.Owner, ent.Comp);

        args.Handled = true;
    }

    private void ConsumeSprintForJump(EntityUid uid, LuaJumpAbilityComponent jump)
    {
        if (!TryComp<LuaSprintComponent>(uid, out var sprint))
            return;

        var cost = sprint.MaxSprint * jump.SprintCostFraction;
        if (cost <= 0f)
            return;

        var wasDepleted = sprint.Depleted;
        sprint.CurrentSprint = MathF.Max(0f, sprint.CurrentSprint - cost);
        sprint.LastSprintTime = _gameTiming.CurTime;
        sprint.Depleted = sprint.CurrentSprint <= 0f;

        if (wasDepleted != sprint.Depleted)
            _movementSpeed.RefreshMovementSpeedModifiers(uid);

        Dirty(uid, sprint);
    }

    private void ApplyJumpImpulse(EntityUid uid, LuaJumpAbilityComponent comp)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics) || physics.BodyType == BodyType.Static)
            return;

        if (_gravity.IsWeightless(uid))
            return;

        if (comp.JumpTarget == null || !IsValidJumper(uid))
            return;

        var fromPos = Transform(uid).Coordinates.ToMapPos(EntityManager, _transform);
        var toPos = comp.JumpTarget.Value.ToMapPos(EntityManager, _transform);
        var direction = (toPos - fromPos).Normalized();

        _physics.ApplyLinearImpulse(uid, direction * comp.Strength * physics.Mass, body: physics);

        comp.TimeUntilNextJump = _gameTiming.CurTime + TimeSpan.FromSeconds(comp.Interval);

        var nearby = _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(comp.JumpTarget.Value), 0.5f);
        if (nearby.Contains(uid))
        {
            comp.IsJumping = false;
            comp.TimeUntilEndJump = _gameTiming.CurTime;
            comp.TimeUntilNextJump = _gameTiming.CurTime;
            Dirty(uid, comp);
        }
    }

    private bool IsValidJumper(EntityUid uid)
    {
        return !HasComp<GhostComponent>(uid)
            && !HasComp<MapGridComponent>(uid)
            && !HasComp<MapComponent>(uid);
    }
}
