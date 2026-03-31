// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.ShipCpu.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Lua.ShipCpu;

public sealed class ShipCpuSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    public const int DefaultMaxTiles = 81;
    public const int DefaultMaxSide = 9;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipCpuComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<ShipCpuComponent, ComponentShutdown>(OnCpuShutdown);
    }

    private void OnAnchorChanged(Entity<ShipCpuComponent> cpu, ref AnchorStateChangedEvent args)
    {
        var xform = Transform(cpu.Owner);
        var gridUid = _transform.GetGrid((cpu.Owner, xform));
        if (gridUid == null || !HasComp<MapGridComponent>(gridUid.Value))
            return;

        RecalculateCpuLimit(gridUid.Value);
    }

    private void OnCpuShutdown(Entity<ShipCpuComponent> cpu, ref ComponentShutdown args)
    {
        var xform = Transform(cpu.Owner);
        var gridUid = _transform.GetGrid((cpu.Owner, xform));
        if (gridUid == null || !HasComp<MapGridComponent>(gridUid.Value))
            return;

        RecalculateCpuLimitExcluding(gridUid.Value, cpu.Owner);
    }

    public void RecalculateCpuLimit(EntityUid gridUid)
    {
        RecalculateCpuLimitExcluding(gridUid, EntityUid.Invalid);
    }

    private void RecalculateCpuLimitExcluding(EntityUid gridUid, EntityUid excludeEntity)
    {
        var maxTiles = DefaultMaxTiles;
        var maxSide = DefaultMaxSide;
        var unlimited = false;
        var cpuQuery = AllEntityQuery<ShipCpuComponent, TransformComponent>();
        while (cpuQuery.MoveNext(out var uid, out var cpu, out var xform))
        {
            if (uid == excludeEntity) continue;
            if (!xform.Anchored) continue;
            if (_transform.GetGrid((uid, xform)) != gridUid) continue;
            if (cpu.Unlimited)
            {
                unlimited = true;
                break;
            }
            if (cpu.MaxTiles > maxTiles) maxTiles = cpu.MaxTiles;
            if (cpu.MaxSide > maxSide) maxSide = cpu.MaxSide;
        }
        var limit = EnsureComp<ShipCpuLimitComponent>(gridUid);
        limit.MaxTiles = maxTiles;
        limit.MaxSide = maxSide;
        limit.Unlimited = unlimited;
        Dirty(gridUid, limit);
    }
}
