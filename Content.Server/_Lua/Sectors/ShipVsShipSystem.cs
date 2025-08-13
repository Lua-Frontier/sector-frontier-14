//REV
// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Random;
using Content.Shared.Parallax;
using Robust.Shared.EntitySerialization;
using Content.Server.LW.ShipVsShipPOI;
using Content.Server.LW.ShipVsShip.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Whitelist;
using Content.Server._Lua.Sectors;

namespace Content.Server.LW.ShipVsShipSector;
public sealed class ShipVsShipRedSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ShipVsShipPOISystem _sectorPOI = default!;

    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    public override void Initialize()
    {
#if DEBUG
        return;
#endif
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<ShipVsShipRedComponent, ComponentStartup>(OnStationRedAdd);
        SubscribeLocalEvent<ShipVsShipBlueComponent, ComponentStartup>(OnStationBlueAdd);
    }

    public void DisableFtl(Entity<FTLDestinationComponent?> ent)
    {
        Log.Info($"Отключение FTL для: {ent}");
        var whitelist = new EntityWhitelist
        {
            RequireAll = false,
        };
        _shuttle.SetFTLWhitelist(ent, whitelist);
    }

    private void OnStationRedAdd(Entity<ShipVsShipRedComponent> ent, ref ComponentStartup args)
    {
        EnsureRedSector(ent);
    }

    private void OnStationBlueAdd(Entity<ShipVsShipBlueComponent> ent, ref ComponentStartup args)
    {
        EnsureBlueSector(ent);
    }

    private EntityUid _redStationGrid = EntityUid.Invalid;
    private MapId _redMapId = MapId.Nullspace;
    private EntityUid _redMapUid = EntityUid.Invalid;

    private EntityUid _blueStationGrid = EntityUid.Invalid;
    private MapId _blueMapId = MapId.Nullspace;
    private EntityUid _blueMapUid = EntityUid.Invalid;

    public void EnsureRedSector(Entity<ShipVsShipRedComponent> source, bool force = false)
    {
        Log.Info("EnsureRedSector");

        if (_redMapId == MapId.Nullspace)
        {
            _redMapUid = _map.CreateMap(out _redMapId, false);
        }

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        _redStationGrid = _gameTicker.MergeGameMap(_prototypeManager.Index(source.Comp.Station), _redMapId, opts).FirstOrNull(HasComp<BecomesStationComponent>)!.Value;
        _metaDataSystem.SetEntityName(_redMapUid, "Сектор Красных");
        EnsureComp<SectorAtmosSupportComponent>(_redMapUid);

        var parallaxes = new[]
        {
        "OriginStation"
        };
        var parallax = EnsureComp<ParallaxComponent>(_redMapUid);
        parallax.Parallax = _random.Pick(parallaxes);

        _sectorPOI.GenerateSectorRedPOIs(_redMapId, _redMapUid, out _);

        if (_redStationGrid.IsValid())
        {
            if (_shuttle.TryAddFTLDestination(_redMapId, false, false, false, out var ftl))
            {
                var entityUid = ftl.Owner;
                DisableFtl((entityUid, ftl));
            }
        }

        _map.InitializeMap(_redMapUid);
    }
    public void EnsureBlueSector(Entity<ShipVsShipBlueComponent> source, bool force = false)
    {
        Log.Info("EnsureBlueSector");

        if (_blueMapId == MapId.Nullspace)
        {
            _blueMapUid = _map.CreateMap(out _blueMapId, false);
        }

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        _blueStationGrid = _gameTicker.MergeGameMap(_prototypeManager.Index(source.Comp.Station), _blueMapId, opts).FirstOrNull(HasComp<BecomesStationComponent>)!.Value;
        _metaDataSystem.SetEntityName(_blueMapUid, "Сектор Синих");
        EnsureComp<SectorAtmosSupportComponent>(_blueMapUid);

        var parallaxes = new[]
        {
        "OriginStation"
        };
        var parallax = EnsureComp<ParallaxComponent>(_blueMapUid);
        parallax.Parallax = _random.Pick(parallaxes);

        _sectorPOI.GenerateSectorBluePOIs(_blueMapId, _blueMapUid, out _);

        if (_blueStationGrid.IsValid())
        {
            if (_shuttle.TryAddFTLDestination(_blueMapId, false, false, false, out var ftl))
            {
                var entityUid = ftl.Owner;
                DisableFtl((entityUid, ftl));
            }
        }

        _map.InitializeMap(_blueMapUid);
    }
    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        Log.Info("OnCleanup");
        QueueDel(_redStationGrid);
        _redStationGrid = EntityUid.Invalid;
        QueueDel(_blueStationGrid);
        _blueStationGrid = EntityUid.Invalid;

        if (_redMapId != MapId.Nullspace && _map.MapExists(_redMapId))
            _map.DeleteMap(_redMapId);

        if (_blueMapId != MapId.Nullspace && _map.MapExists(_blueMapId))
            _map.DeleteMap(_blueMapId);

        _redMapId = MapId.Nullspace;
        _redMapUid = EntityUid.Invalid;
        _redStationGrid = EntityUid.Invalid;

        _blueMapId = MapId.Nullspace;
        _blueMapUid = EntityUid.Invalid;
        _blueStationGrid = EntityUid.Invalid;
    }
}
