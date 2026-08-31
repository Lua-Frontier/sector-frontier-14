// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using System.Diagnostics.CodeAnalysis;
using Content.Server._Lua.Shuttles.Components;
using Content.Server._NF.Worldgen.Components.Debris;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Server.Worldgen.Components.GC;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Shuttles.Systems;

public enum ShuttleGridKind
{
    Shuttle,
    Station,
    Event,
    ShuttleAi,
    Debris,
    Wrecks,
}

[UsedImplicitly]
public sealed class ShuttleGridAccessSystem : EntitySystem
{
    private static readonly EntProtoId BaseScrapDebrisId = "BaseScrapDebris";
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private EntityQuery<ShuttleGridComponent> _shuttleGridQuery;
    private EntityQuery<StationGridComponent> _stationGridQuery;
    private EntityQuery<EventGridComponent> _eventGridQuery;
    private EntityQuery<ShuttleAiGridComponent> _shuttleAiGridQuery;
    private EntityQuery<DebrisGridComponent> _debrisGridQuery;
    private EntityQuery<WrecksGridComponent> _wrecksGridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _shuttleGridQuery = GetEntityQuery<ShuttleGridComponent>();
        _stationGridQuery = GetEntityQuery<StationGridComponent>();
        _eventGridQuery = GetEntityQuery<EventGridComponent>();
        _shuttleAiGridQuery = GetEntityQuery<ShuttleAiGridComponent>();
        _debrisGridQuery = GetEntityQuery<DebrisGridComponent>();
        _wrecksGridQuery = GetEntityQuery<WrecksGridComponent>();
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<ShuttleComponent, ComponentInit>(OnLegacyShuttleInit);
        SubscribeLocalEvent<ShuttleComponent, MapInitEvent>(OnLegacyShuttleMapInit);
    }

    public ShuttleGridKind? GetKind(EntityUid uid)
    {
        if (_shuttleAiGridQuery.HasComponent(uid))
            return ShuttleGridKind.ShuttleAi;
        if (_stationGridQuery.HasComponent(uid))
            return ShuttleGridKind.Station;
        if (_wrecksGridQuery.HasComponent(uid))
            return ShuttleGridKind.Wrecks;
        if (_debrisGridQuery.HasComponent(uid))
            return ShuttleGridKind.Debris;
        if (_eventGridQuery.HasComponent(uid))
            return ShuttleGridKind.Event;
        if (_shuttleGridQuery.HasComponent(uid))
            return ShuttleGridKind.Shuttle;
        return null;
    }

    public bool IsPilotableGrid(EntityUid uid) => IsMobileShuttle(uid);

    public bool IsMobileShuttle(EntityUid uid)
    {
        return _shuttleGridQuery.HasComponent(uid)
            || _shuttleAiGridQuery.HasComponent(uid)
            || _stationGridQuery.HasComponent(uid);
    }

    public bool IsDebrisKind(ShuttleGridKind kind) => kind is ShuttleGridKind.Debris or ShuttleGridKind.Wrecks;

    public bool HasFtlGrid(EntityUid uid)
    {
        var kind = GetKind(uid);
        return kind is ShuttleGridKind.Shuttle or ShuttleGridKind.ShuttleAi or ShuttleGridKind.Event or ShuttleGridKind.Station;
    }

    public bool HasAnyGridType(EntityUid uid) => GetKind(uid) != null;

    public bool TryGetShuttleGrid(EntityUid uid, [NotNullWhen(true)] out IShuttleGrid? grid)
    {
        if (_shuttleGridQuery.TryGetComponent(uid, out var shuttle))
        {
            grid = shuttle;
            return true;
        }
        if (_stationGridQuery.TryGetComponent(uid, out var station))
        {
            grid = station;
            return true;
        }
        if (_eventGridQuery.TryGetComponent(uid, out var ev))
        {
            grid = ev;
            return true;
        }
        if (_shuttleAiGridQuery.TryGetComponent(uid, out var ai))
        {
            grid = ai;
            return true;
        }
        if (_wrecksGridQuery.TryGetComponent(uid, out var wrecks))
        {
            grid = wrecks;
            return true;
        }
        if (_debrisGridQuery.TryGetComponent(uid, out var debris))
        {
            grid = debris;
            return true;
        }
        grid = null;
        return false;
    }

    public ShuttleGridKind ResolveGridType(EntityUid uid)
    {
        if (_shuttleAiGridQuery.HasComponent(uid))
            return ShuttleGridKind.ShuttleAi;
        if (HasComp<BecomesStationComponent>(uid))
            return ShuttleGridKind.Station;
        if (_wrecksGridQuery.HasComponent(uid))
            return ShuttleGridKind.Wrecks;
        if (HasComp<SpaceDebrisComponent>(uid) || IsDebrisGc(uid))
            return IsWreckDebris(uid) ? ShuttleGridKind.Wrecks : ShuttleGridKind.Debris;
        if (HasComp<LinkedLifecycleGridParentComponent>(uid) || HasComp<LinkedLifecycleGridChildComponent>(uid))
            return ShuttleGridKind.Event;
        return ShuttleGridKind.Shuttle;
    }

    public void EnsureGridType(EntityUid uid, ShuttleGridKind kind, IShuttleGrid? copyFrom = null)
    {
        RemoveOtherGridTypes(uid, kind);
        var grid = EnsureGridComponent(uid, kind);
        if (copyFrom != null)
            ShuttleGridPhysicsFields.CopyFrom(grid, copyFrom);
    }

    public void RemoveOtherGridTypes(EntityUid uid, ShuttleGridKind keep)
    {
        if (keep != ShuttleGridKind.Shuttle && _shuttleGridQuery.HasComponent(uid))
            RemComp<ShuttleGridComponent>(uid);
        if (keep != ShuttleGridKind.Station && _stationGridQuery.HasComponent(uid))
            RemComp<StationGridComponent>(uid);
        if (keep != ShuttleGridKind.Event && _eventGridQuery.HasComponent(uid))
            RemComp<EventGridComponent>(uid);
        if (keep != ShuttleGridKind.ShuttleAi && _shuttleAiGridQuery.HasComponent(uid))
            RemComp<ShuttleAiGridComponent>(uid);
        if (keep != ShuttleGridKind.Debris && _debrisGridQuery.HasComponent(uid))
            RemComp<DebrisGridComponent>(uid);
        if (keep != ShuttleGridKind.Wrecks && _wrecksGridQuery.HasComponent(uid))
            RemComp<WrecksGridComponent>(uid);
    }

    public void InitializeGrid(EntityUid uid)
    {
        if (HasComp<MapComponent>(uid))
            return;
        if (TryMigrateLegacyShuttle(uid))
            return;
        if (HasAnyGridType(uid))
            return;
        EnsureGridType(uid, ResolveGridType(uid));
    }

    private void OnLegacyShuttleInit(EntityUid uid, ShuttleComponent comp, ComponentInit args) => TryMigrateLegacyShuttle(uid);

    private void OnLegacyShuttleMapInit(EntityUid uid, ShuttleComponent comp, MapInitEvent args) => TryMigrateLegacyShuttle(uid);

    private bool TryMigrateLegacyShuttle(EntityUid uid)
    {
        if (!TryComp<ShuttleComponent>(uid, out var legacy))
            return false;
        if (HasComp<MapComponent>(uid))
        {
            RemComp<ShuttleComponent>(uid);
            return true;
        }
        EnsureGridType(uid, ShuttleGridKind.Shuttle, legacy);
        RemComp<ShuttleComponent>(uid);
        return true;
    }

    private void OnGridSplit(ref GridSplitEvent ev)
    {
        if (!TryGetShuttleGrid(ev.Grid, out var parentGrid))
            return;
        var parentKind = GetKind(ev.Grid);
        if (parentKind == null)
            return;
        foreach (var child in ev.NewGrids)
        {
            if (child == ev.Grid)
                continue;
            EnsureGridType(child, parentKind.Value, parentGrid);
        }
    }

    private IShuttleGrid EnsureGridComponent(EntityUid uid, ShuttleGridKind kind)
    {
        return kind switch
        {
            ShuttleGridKind.Station => EnsureComp<StationGridComponent>(uid),
            ShuttleGridKind.Event => EnsureComp<EventGridComponent>(uid),
            ShuttleGridKind.ShuttleAi => EnsureComp<ShuttleAiGridComponent>(uid),
            ShuttleGridKind.Debris => EnsureComp<DebrisGridComponent>(uid),
            ShuttleGridKind.Wrecks => EnsureComp<WrecksGridComponent>(uid),
            _ => EnsureComp<ShuttleGridComponent>(uid),
        };
    }

    private bool IsWreckDebris(EntityUid uid)
    {
        if (_wrecksGridQuery.HasComponent(uid))
            return true;
        if (!TryComp<MetaDataComponent>(uid, out var meta) || meta.EntityPrototype is not { } proto)
            return false;
        return InheritsFrom(proto.ID, BaseScrapDebrisId);
    }

    private bool InheritsFrom(string protoId, EntProtoId ancestorId)
    {
        if (!_prototypes.TryIndex(protoId, out EntityPrototype? proto))
            return false;
        if (proto.ID == ancestorId)
            return true;
        if (proto.Parents == null)
            return false;
        foreach (var parent in proto.Parents)
        {
            if (InheritsFrom(parent, ancestorId))
                return true;
        }
        return false;
    }

    private bool IsDebrisGc(EntityUid uid)
    {
        if (!TryComp<GCAbleObjectComponent>(uid, out var gc))
            return false;
        return gc.Queue == "SpaceDebris";
    }

    public static bool TryParseGridKind(string value, out ShuttleGridKind kind)
    {
        kind = default;
        switch (value.Trim().ToLowerInvariant())
        {
            case "shuttle":
                kind = ShuttleGridKind.Shuttle;
                return true;
            case "station":
                kind = ShuttleGridKind.Station;
                return true;
            case "event":
                kind = ShuttleGridKind.Event;
                return true;
            case "shuttleai":
                kind = ShuttleGridKind.ShuttleAi;
                return true;
            default:
                return false;
        }
    }
}

public delegate void ShuttleGridEventHandler<in TEvent>(EntityUid uid, IShuttleGrid grid, TEvent args) where TEvent : notnull;

public delegate void ShuttleGridRefEventHandler<TEvent>(EntityUid uid, IShuttleGrid grid, ref TEvent args) where TEvent : struct;
