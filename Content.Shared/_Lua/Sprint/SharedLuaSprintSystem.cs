// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Lua.Sprint;

public abstract class SharedLuaSprintSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LuaSprintComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
    }

    private void OnRefreshMoveSpeed(Entity<LuaSprintComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Depleted)
            return;

        if (!TryComp<MovementSpeedModifierComponent>(ent, out var move))
            return;

        if (move.BaseSprintSpeed <= 0f)
            return;

        // When exhausted, sprint speed collapses to walk speed.
        var sprintModifier = MathF.Min(1f, move.BaseWalkSpeed / move.BaseSprintSpeed);
        args.ModifySpeed(1f, sprintModifier);
    }
}
