// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Linq;
using Content.Server.LW.TypanSector.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Server.Worldgen.Prototypes;
using Content.Shared.Shuttles.Components;
using Content.Shared.Whitelist;
using Content.Shared.CCVar;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Random;
using Content.Shared.Parallax;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map.Components;
using System.Numerics;
using Content.Server.LW.TypanSectorPOI;
using Content.Shared.Lua.CLVar;
using Content.Server._Lua.Sectors;

namespace Content.Server.LW.TypanSector;
public sealed class TypanSectorSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly ISerializationManager _ser = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TypanSectorPOISystem _sectorPOI = default!;

    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    private bool _worldgenEnabled;
    private string _asteroidConfig = "Default";
    private bool _asteroidSectorEnabled = true;
    public override void Initialize()
    {
#if DEBUG
        return;
#endif
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<TypanSectorComponent, ComponentStartup>(OnStationAdd);

        _cfg.OnValueChanged(CCVars.WorldgenEnabled, OnWorldgenEnabledChanged, true);
        _cfg.OnValueChanged(CLVars.AsteroidSectorEnabled, OnAsteroidSectorEnabledChanged, true);
    }

    private void OnAsteroidSectorEnabledChanged(bool enabled)
    {
        _asteroidSectorEnabled = enabled;

        if (!enabled && _mapId != MapId.Nullspace)
        {
            ForceCleanup();
        }
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        if (!_worldgenEnabled)
            return;

        if (_mapUid == EntityUid.Invalid)
        {
            Log.Error("Ошибка: Сектор не инициализирован перед генерацией!");
            return;
        }

        if (!_prototypeManager.TryIndex<WorldgenConfigPrototype>(_asteroidConfig, out var config))
        {
            Log.Error($"Конфиг '{_asteroidConfig}' не найден!");
            return;
        }

        config.Apply(_mapUid, _ser, EntityManager);
        Log.Info("Генерация применена к сектору");
    }

    public void DisableFtl(Entity<FTLDestinationComponent?> ent)
    {
        Log.Info($"Отключение FTL для: {ent}");
        var whitelist = new EntityWhitelist
        {
            RequireAll = false,
            Components = new[] { "TypanFtl" }
        };
        _shuttle.SetFTLWhitelist(ent, whitelist);
    }

    public void EnableFtl(Entity<FTLDestinationComponent?> ent)
    {
        Log.Info($"Включение FTL для: {ent}");
        _shuttle.SetFTLWhitelist(ent, null);
    }

    private void OnWorldgenEnabledChanged(bool enabled)
    {
        _worldgenEnabled = enabled;
    }

    private void OnStationAdd(Entity<TypanSectorComponent> ent, ref ComponentStartup args)
    {
        EnsureTypanSector(ent);
    }

    private EntityUid _stationGrid = EntityUid.Invalid;
    private MapId _mapId = MapId.Nullspace;
    private EntityUid _mapUid = EntityUid.Invalid;
    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        Log.Info("OnCleanup");
        QueueDel(_stationGrid);
        _stationGrid = EntityUid.Invalid;

        if (_map.MapExists(_mapId))
            _map.DeleteMap(_mapId);

        _mapId = MapId.Nullspace;
        _mapUid = EntityUid.Invalid;
    }
    public void EnsureTypanSector(Entity<TypanSectorComponent> source, bool force = false)
    {
        Log.Info("EnsureTypanSector");
        if (!_asteroidSectorEnabled)
        {
            return;
        }
        if (!source.Comp.Enabled)
        {
            Log.Info("Инициализация сектора отключена (Enabled = false)");
            return;
        }
        if (_stationGrid.IsValid())
        {
            return;
        }

        Log.Info("Запуск сектора Нордфолл");
        if (_mapId == MapId.Nullspace)
        {
            _mapUid = _map.CreateMap(out _mapId, false);
        }

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        _stationGrid = _gameTicker.MergeGameMap(_prototypeManager.Index(source.Comp.Station), _mapId, opts).FirstOrNull(HasComp<BecomesStationComponent>)!.Value;
        _metaDataSystem.SetEntityName(_mapUid, "Сектор Нордфолл");
        EnsureComp<SectorAtmosSupportComponent>(_mapUid);

        var parallaxes = new[]
        {
        "OriginStation",
        "NordFoll"
        };
        var parallax = EnsureComp<ParallaxComponent>(_mapUid);
        parallax.Parallax = _random.Pick(parallaxes);

        _sectorPOI.GenerateSectorPOIs(_mapId, _mapUid, out _);

        #region DeadCode
        //var locations = new (ResPath Path, string Name, Color Color, Vector2 Offset)[]
        //{
        //    (new ResPath("/Maps/_Lua/POI/typancargodepot.yml"), "Чёрный Рынок", new Color(255, 61, 80), _random.NextVector2(2500f, 2000f)),
        //};

        //foreach (var (path, name, color, offset) in locations)
        //{
        //    if (_loader.TryLoadGrid(_mapId, path, out Entity<MapGridComponent>? grid, new DeserializationOptions()) && grid != null)
        //    {
        //        var meta = EnsureComp<MetaDataComponent>(grid.Value.Owner);
        //        _meta.SetEntityName(grid.Value.Owner, name, meta);
        //        _shuttle.SetIFFColor(grid.Value.Owner, color);
        //    }
        //} //DeadCode
        #endregion DeadCode

        if (_stationGrid.IsValid())
        {
            if (_shuttle.TryAddFTLDestination(_mapId, true, false, false, out var ftl))
            {
                var entityUid = ftl.Owner;
                DisableFtl((entityUid, ftl));
            }
        }
        _map.InitializeMap(_mapUid);
    }

    private void ForceCleanup()
    {
        QueueDel(_stationGrid);
        _stationGrid = EntityUid.Invalid;
        if (_map.MapExists(_mapId))
            _map.DeleteMap(_mapId);
        _mapId = MapId.Nullspace;
        _mapUid = EntityUid.Invalid;
    }
}
