// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Lua.Shared.Shuttles.Components;
using Content.Server.StationEvents.Events;
using Robust.Shared.Timing;

namespace Content.Lua.Server.Shuttles.Systems;

public sealed class ShuttleAiGridLifetimeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleAiGridComponent, MapInitEvent>(OnMapInit);
        _nextCheck = _timing.CurTime + CheckInterval;
    }

    private void OnMapInit(EntityUid uid, ShuttleAiGridComponent component, MapInitEvent args)
    {
        component.SpawnTime = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ShuttleAiGridComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SpawnTime == TimeSpan.Zero)
                comp.SpawnTime = now;

            if (now - comp.SpawnTime < comp.MaxLifetime)
                continue;

            if (TerminatingOrDeleted(uid))
                continue;

            _linkedLifecycleGrid.UnparentPlayersFromGrid(uid, deleteGrid: true);
        }
    }
}
