// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Lua.Shared.Shuttles.Components;
using Content.Lua.Server.Shipyard.Components;
using Content.Server.Physics.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._Mono.Company;
using Content.Shared._Mono.Ships.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Lua.Server.Shuttles.Systems;

public sealed class ShuttleWreckLifetimeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(15);
    private const string DefaultGridName = "grid";

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        _nextCheck = _timing.CurTime + CheckInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ShuttleGridComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (!IsNamelessWreck(uid, meta))
            {
                if (HasComp<ShuttleWreckLifetimeComponent>(uid))
                    RemCompDeferred<ShuttleWreckLifetimeComponent>(uid);
                continue;
            }

            var marker = EnsureComp<ShuttleWreckLifetimeComponent>(uid);
            if (marker.DetectedAt == TimeSpan.Zero)
                marker.DetectedAt = now;

            if (now - marker.DetectedAt < MaxLifetime)
                continue;

            _linkedLifecycleGrid.UnparentPlayersFromGrid(uid, deleteGrid: true);
        }
    }

    private bool IsNamelessWreck(EntityUid uid, MetaDataComponent meta)
    {
        if (HasComp<MapComponent>(uid) || HasComp<BecomesStationComponent>(uid))
            return false;

        if (!string.Equals(meta.EntityName.Trim(), DefaultGridName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (HasComp<ShuttleDeedComponent>(uid)
            || HasComp<ShipOwnershipComponent>(uid)
            || HasComp<VesselComponent>(uid)
            || HasComp<StationMemberComponent>(uid)
            || HasComp<PilotedShuttleComponent>(uid)
            || HasComp<ParkedShuttleComponent>(uid)
            || HasComp<CompanyComponent>(uid))
            return false;

        return !HasPlayersOnGrid(uid);
    }

    private bool HasPlayersOnGrid(EntityUid gridUid)
    {
        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == gridUid)
                return true;
        }

        var mindQuery = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        while (mindQuery.MoveNext(out _, out var mind, out var xform))
        {
            if (xform.GridUid == gridUid && mind.HasMind)
                return true;
        }

        return false;
    }
}
