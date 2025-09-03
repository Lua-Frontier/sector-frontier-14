// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Linq;
using Content.Server.LW.AsteroidSector.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Maps;
using Content.Server.Worldgen.Prototypes;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Shuttles.Components;
using Content.Shared.Whitelist;
using Content.Shared.Lua.CLVar;
using Content.Shared.Parallax;
using Content.Shared.Alert;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map.Components;
using System.Numerics;
using Content.Server.LW.AsteroidSectorPOI;
using Content.Server._Lua.Sectors;

namespace Content.Server.LW.AsteroidSector;
public sealed class AsteroidSectorSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly ISerializationManager _ser = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly AsteroidSectorPOISystem _sectorPOI = default!;

    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    private bool _pvpEnabled = true;
    private bool _asteroidSectorEnabled = true;

    private bool _worldgenEnabled;
    private string _asteroidConfig = "AsteroidSectorDefault";
    public override void Initialize()
    {
#if DEBUG
        return;
#endif
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<AsteroidSectorComponent, ComponentStartup>(OnStationAdd);

        _cfg.OnValueChanged(CCVars.WorldgenEnabled, OnWorldgenEnabledChanged, true);
        _cfg.OnValueChanged(CLVars.PvpEnabled, OnPvpEnabledChanged, true);

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

    private void OnPvpEnabledChanged(bool value)
    {
        _pvpEnabled = value;

        if (!_pvpEnabled)
        {
            ClearAllPvpAlerts();
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
            Components = new[] { "NanotrasenFtl", "MercenaryFtl", "PirateFtl", "TypanFtl" }
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

    private void OnStationAdd(Entity<AsteroidSectorComponent> ent, ref ComponentStartup args)
    {
        EnsureAsteroidSector(ent);
    }

    private EntityUid _stationGrid = EntityUid.Invalid;
    private MapId _mapId = MapId.Nullspace;
    private EntityUid _mapUid = EntityUid.Invalid;

    private readonly HashSet<EntityUid> _trackedPvp = new();

    private TimeSpan _nextPvpCheck;
    private static readonly TimeSpan PvpCheckInterval = TimeSpan.FromSeconds(20);

    private const string PvpAlertType = "Pvp";

    public override void Update(float frameTime)
    {
        if (!_pvpEnabled)
            return;

        base.Update(frameTime);

        if (_gameTiming.CurTime < _nextPvpCheck)
            return;

        _nextPvpCheck = _gameTiming.CurTime + PvpCheckInterval;

        if (_mapId == MapId.Nullspace || !_mapManager.MapExists(_mapId))
        {
            ClearAllPvpAlerts();
            return;
        }

        var newEntitiesOnAsteroid = new List<EntityUid>();
        var query = AllEntityQuery<HumanoidAppearanceComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out _, out var xform))
        {
            if (xform.MapID == _mapId)
            {
                if (!_mindSystem.TryGetMind(ent, out _, out _))
                    continue;

                newEntitiesOnAsteroid.Add(ent);
            }
        }
        var exited = _trackedPvp.Except(newEntitiesOnAsteroid);
        foreach (var ex in exited)
        {
            DisableAlert(ex);
        }
        var entered = newEntitiesOnAsteroid.Except(_trackedPvp);
        foreach (var e in entered)
        {
            EnableAlert(e);
        }
        _trackedPvp.Clear();
        _trackedPvp.UnionWith(newEntitiesOnAsteroid);
    }

    private void EnableAlert(EntityUid entity)
    {
        _alerts.ShowAlert(entity, PvpAlertType);
    }
    private void ClearAllPvpAlerts()
    {
        foreach (var e in _trackedPvp)
        {
            DisableAlert(e);
        }
        _trackedPvp.Clear();
    }

    private void DisableAlert(EntityUid entity)
    {
        _alerts.ClearAlert(entity, PvpAlertType);
    }


    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        Log.Info("OnCleanup");
        QueueDel(_stationGrid);
        _stationGrid = EntityUid.Invalid;
        if (_mapSystem.MapExists(_mapId))
            _mapSystem.DeleteMap(_mapId);
        _mapId = MapId.Nullspace;
        _mapUid = EntityUid.Invalid;

        ClearAllPvpAlerts();
    }

    public MapId GetAsteroidSectorMapId()
    {
        if (_mapId == MapId.Nullspace)
        {
            Log.Warning("Попытка получить ID карты, но карта еще не создана.");
        }
        return _mapId;
    }
    public void EnsureAsteroidSector(Entity<AsteroidSectorComponent> source, bool force = false)
    {
        Log.Info("EnsureAsteroidSector");

        if (!_asteroidSectorEnabled)
        {
            return;
        }

        if (!source.Comp.Enabled)
        {
            Log.Info("Инициализация сектора отключена");
            return;
        }

        if (_stationGrid.IsValid())
        {
            return;
        }
        Log.Info("Start load Asteroid Sector");

        if (_mapId == MapId.Nullspace)
        {
            _mapUid = _mapSystem.CreateMap(out _mapId, false);
        }

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        _stationGrid = _gameTicker.MergeGameMap(_prototypeManager.Index(source.Comp.Station), _mapId, opts).FirstOrNull(HasComp<BecomesStationComponent>)!.Value;
        _metaDataSystem.SetEntityName(_mapUid, "Поле Астероидов");
        EnsureComp<SectorAtmosSupportComponent>(_mapUid);

        var parallaxes = new[]
        {
        "AspidParallax",
        "BagelStation",
        "CoreStation",
        "OriginStation",
        "TrainStation",
        "KettleStation",
        "Sky",
        "Asteroids",
        "Default"
        };
        var parallax = EnsureComp<ParallaxComponent>(_mapUid);
        parallax.Parallax = _random.Pick(parallaxes);


        _sectorPOI.GenerateSectorPOIs(_mapId, _mapUid, out _);

        #region DeadCode
        //var locations = new (ResPath Path, string Name, Color Color, Vector2 Offset)[]
        //{
        //    (new ResPath("/Maps/_NF/POI/cargodepot.yml"), "Торговый Терминал А", new Color(55, 200, 55), _random.NextVector2(2500f, 3000f)),
        //    (new ResPath("/Maps/_NF/POI/cargodepot.yml"), "Торговый Терминал Б", new Color(55, 200, 55), _random.NextVector2(2500f, 3000f)),
        //    (new ResPath("/Maps/_Lua/POI/anomalouslab.yml"), "Лаборатория Аномалий", new Color(255, 165, 0), _random.NextVector2(2100f, 3800f)),
        //    (new ResPath("/Maps/_Lua/POI/asteroidtradeoutpost.yml"), "Чёрный Рынок", new Color(255, 61, 80), _random.NextVector2(1500f, 1000f)),
        //};

        //foreach (var (path, name, color, offset) in locations)
        //{
        //    if (_loader.TryLoadGrid(_mapId, path, out Entity<MapGridComponent>? grid, new DeserializationOptions()) && grid != null)
        //    {
        //        var gridEntity = grid.Value.Owner;
        //        if (EntityManager.TryGetComponent<TransformComponent>(gridEntity, out var transform))
        //        {
        //            transform.WorldPosition = offset;
        //        }

        //        var meta = EnsureComp<MetaDataComponent>(grid.Value.Owner);
        //        _meta.SetEntityName(grid.Value.Owner, name, meta);
        //        _shuttle.SetIFFColor(grid.Value.Owner, color);

        //        if (path.CanonPath == "/Maps/_Lua/POI/asteroidtradeoutpost.yml")
        //        {
        //            if (_prototypeManager.TryIndex<GameMapPrototype>("AsteroidTradeOutpost", out var stationProto))
        //            {
        //                _station.InitializeNewStation(stationProto.Stations["AsteroidTradeOutpost"], new[] { gridEntity });
        //            }
        //        }
        //    }
        //}
        #endregion DeadCode

        if (_stationGrid.IsValid())
        {
            if (_shuttle.TryAddFTLDestination(_mapId, true, false, false, out var ftl))
            {
                var entityUid = ftl.Owner;
                DisableFtl((entityUid, ftl));
            }
        }
        _mapSystem.InitializeMap(_mapUid);
    }
    private void ForceCleanup()
    {
        QueueDel(_stationGrid);
        _stationGrid = EntityUid.Invalid;
        if (_mapSystem.MapExists(_mapId))
            _mapSystem.DeleteMap(_mapId);
        _mapId = MapId.Nullspace;
        _mapUid = EntityUid.Invalid;
        ClearAllPvpAlerts();
    }
}

