// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Cloning.Events;
using Content.Shared.Gravity;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared._Lua.Sprint;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Shared._Lua.JumpAbility;

public abstract partial class SharedLuaJumpAbilitySystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LuaJumpAbilityComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<LuaJumpAbilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LuaJumpAbilityComponent, LuaDirectionalJumpEvent>(OnDirectionalJump);
        SubscribeLocalEvent<LuaJumpAbilityComponent, CloningEvent>(OnClone);

        SubscribeLocalEvent<ActiveLeaperComponent, StartCollideEvent>(OnLeaperCollide);
        SubscribeLocalEvent<ActiveLeaperComponent, LandEvent>(OnLeaperLand);
        SubscribeLocalEvent<ActiveLeaperComponent, StopThrowEvent>(OnLeaperStopThrow);
    }

    private void OnInit(Entity<LuaJumpAbilityComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent, out ActionsComponent? comp))
            return;

        if (ent.Comp.DirectionalAction != null)
            Actions.AddAction(ent, ref ent.Comp.DirectionalActionEntity, ent.Comp.DirectionalAction, component: comp);

        if (ent.Comp.PointAction != null)
            Actions.AddAction(ent, ref ent.Comp.PointActionEntity, ent.Comp.PointAction, component: comp);
    }

    private void OnShutdown(Entity<LuaJumpAbilityComponent> ent, ref ComponentShutdown args)
    {
        Actions.RemoveAction(ent.Owner, ent.Comp.DirectionalActionEntity);
        Actions.RemoveAction(ent.Owner, ent.Comp.PointActionEntity);
    }

    protected virtual void OnDirectionalJump(Entity<LuaJumpAbilityComponent> ent, ref LuaDirectionalJumpEvent args)
    {
        if (_gravity.IsWeightless(args.Performer) || _standing.IsDown(args.Performer) || !HasEnoughSprintForJump(args.Performer, ent.Comp))
        {
            if (ent.Comp.JumpFailedPopup != null)
                _popup.PopupClient(Loc.GetString(ent.Comp.JumpFailedPopup.Value), args.Performer, args.Performer);
            return;
        }

        var xform = Transform(args.Performer);
        var direction = xform.Coordinates.Offset(xform.LocalRotation.ToWorldVec() * ent.Comp.JumpDistance);
        _throwing.TryThrow(args.Performer, direction, ent.Comp.JumpThrowSpeed);
        Audio.PlayPredicted(ent.Comp.JumpSound, args.Performer, args.Performer);

        if (ent.Comp.CanCollide)
        {
            EnsureComp<ActiveLeaperComponent>(ent, out var leaper);
            leaper.KnockdownDuration = ent.Comp.CollideKnockdown;
            Dirty(ent.Owner, leaper);
        }

        args.Handled = true;
    }

    protected bool HasEnoughSprintForJump(EntityUid uid, LuaJumpAbilityComponent jump)
    {
        if (!TryComp<LuaSprintComponent>(uid, out var sprint)) return true;
        if (sprint.Depleted) return false;
        var cost = sprint.MaxSprint * jump.SprintCostFraction;
        return sprint.CurrentSprint >= cost;
    }

    private void OnLeaperCollide(Entity<ActiveLeaperComponent> ent, ref StartCollideEvent args)
    {
        _stun.TryKnockdown(ent.Owner, ent.Comp.KnockdownDuration, force: true);
        RemCompDeferred<ActiveLeaperComponent>(ent);
    }

    private void OnLeaperLand(Entity<ActiveLeaperComponent> ent, ref LandEvent args)
    {
        RemCompDeferred<ActiveLeaperComponent>(ent);
    }

    private void OnLeaperStopThrow(Entity<ActiveLeaperComponent> ent, ref StopThrowEvent args)
    {
        RemCompDeferred<ActiveLeaperComponent>(ent);
    }

    private void OnClone(Entity<LuaJumpAbilityComponent> ent, ref CloningEvent args)
    {
        if (!args.Settings.EventComponents.Contains(Factory.GetRegistration(ent.Comp.GetType()).Name))
            return;

        var clone = Factory.GetComponent<LuaJumpAbilityComponent>();
        clone.DirectionalAction = ent.Comp.DirectionalAction;
        clone.PointAction = ent.Comp.PointAction;
        clone.JumpDistance = ent.Comp.JumpDistance;
        clone.JumpThrowSpeed = ent.Comp.JumpThrowSpeed;
        clone.CanCollide = ent.Comp.CanCollide;
        clone.CollideKnockdown = ent.Comp.CollideKnockdown;
        clone.Strength = ent.Comp.Strength;
        clone.JumpDuration = ent.Comp.JumpDuration;
        clone.Interval = ent.Comp.Interval;
        clone.JumpSound = ent.Comp.JumpSound;
        AddComp(args.CloneUid, clone, true);
    }
}
