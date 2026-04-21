// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Shared._Lua.Stargate.Components;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargatePortalAutoCloseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StargateSystem _stargate = default!;

    private float _checkAccumulator;
    private const float CheckInterval = 1f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _checkAccumulator += frameTime;
        if (_checkAccumulator < CheckInterval)
            return;

        _checkAccumulator -= CheckInterval;

        var curTime = _timing.CurTime;
        var query = AllEntityQuery<StargatePortalTimerComponent, StargateComponent>();
        var toClose = new List<EntityUid>();

        while (query.MoveNext(out var uid, out var timer, out var gate))
        {
            if (!timer.HasEntityPassedThrough)
                continue;

            if (curTime - timer.LastEntityNearTime >= TimeSpan.FromSeconds(timer.CloseDelay))
                toClose.Add(uid);
        }

        foreach (var uid in toClose)
        {
            _stargate.ClosePortal(uid);
        }
    }
}
