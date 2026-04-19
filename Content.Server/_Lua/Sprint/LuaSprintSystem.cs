// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Sprint;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Sprint;

public sealed class LuaSprintSystem : SharedLuaSprintSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeed = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<LuaSprintComponent, InputMoverComponent>();
        while (query.MoveNext(out var uid, out var endurance, out var mover))
        {
            var oldSprint = endurance.CurrentSprint;
            var hadDepleted = endurance.Depleted;
            var wasSprinting = mover.CanMove && mover.Sprinting && mover.HasDirectionalMovement && !endurance.Depleted;

            if (wasSprinting)
            {
                endurance.CurrentSprint = MathF.Max(0f, endurance.CurrentSprint - endurance.DrainPerSecond * frameTime);
                endurance.LastSprintTime = curTime;

                if (endurance.CurrentSprint <= 0f)
                    endurance.Depleted = true;
            }
            else
            {
                if (curTime >= endurance.LastSprintTime + TimeSpan.FromSeconds(endurance.RegenDelay))
                    endurance.CurrentSprint = MathF.Min(endurance.MaxSprint, endurance.CurrentSprint + endurance.RegenPerSecond * frameTime);

                if (endurance.Depleted && endurance.CurrentSprint >= endurance.MaxSprint * endurance.RecoverThresholdFraction)
                    endurance.Depleted = false;
            }

            if (oldSprint != endurance.CurrentSprint || endurance.Depleted != hadDepleted)
                Dirty(uid, endurance);

            if (endurance.Depleted != hadDepleted)
                _moveSpeed.RefreshMovementSpeedModifiers(uid);
        }
    }
}
