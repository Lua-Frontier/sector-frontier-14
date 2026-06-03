// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Lua.Sprint;

public abstract class SharedLuaSprintSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeed = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LuaSprintComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<LuaSprintComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<LuaSprintComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<LuaSprintComponent> ent, ref ComponentStartup args)
    { _moveSpeed.RefreshMovementSpeedModifiers(ent); }
    private void OnMoveInput(Entity<LuaSprintComponent> ent, ref MoveInputEvent args)
    {
        var wasSprintHeld = (args.OldMovement & MoveButtons.Sprint) != 0;
        var nowSprintHeld = args.Entity.Comp.IsSprinting;
        if (wasSprintHeld != nowSprintHeld)
            _moveSpeed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRefreshMoveSpeed(Entity<LuaSprintComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Depleted) return;
        if (!TryComp<MovementSpeedModifierComponent>(ent, out var move)) return;
        var isSprinting = TryComp<InputMoverComponent>(ent, out var mover) && mover.IsSprinting;
        var activeBase = isSprinting ? move.BaseSprintSpeed : move.BaseRunningSpeed;
        if (activeBase <= 0f) return;

        var depletedMod = MathF.Min(1f, move.BaseWalkSpeed / activeBase);
        args.ModifySpeed(1f, depletedMod);
    }
}
