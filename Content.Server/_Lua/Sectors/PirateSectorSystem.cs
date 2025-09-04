// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Linq;
using Content.Server.LW.PirateSector.Components;
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
using Content.Shared.Shuttles.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Serialization.Manager;
using Content.Shared.Parallax;
using Robust.Shared.Random;
using Robust.Shared.EntitySerialization;
using Content.Shared.Lua.CLVar;
using Content.Server._Lua.Sectors;

namespace Content.Server.LW.PirateSector;
public sealed class PirateSectorSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly ISerializationManager _ser = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    private bool _asteroidSectorEnabled = true;

    public override void Initialize()
    {
#if DEBUG
        return;
#endif
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<PirateSectorComponent, ComponentStartup>(OnStationAdd);
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

    public void DisableFtl(Entity<FTLDestinationComponent?> ent)
    {
        Log.Info($"Отключение FTL для: {ent}");
        var whitelist = new EntityWhitelist
        {
            RequireAll = false,
            Components = new[] { "PirateFtl" }
            //Components = new[] { "PirateFtl", "MercenaryFtl", "TypanFtl" }
        };
        _shuttle.SetFTLWhitelist(ent, whitelist);
    }

    public void EnableFtl(Entity<FTLDestinationComponent?> ent)
    {
        Log.Info($"Включение FTL для: {ent}");
        _shuttle.SetFTLWhitelist(ent, null);
    }
    private void OnStationAdd(Entity<PirateSectorComponent> ent, ref ComponentStartup args)
    {
        EnsurePirateSector(ent);
    }

    private EntityUid _stationGrid = EntityUid.Invalid;
    private MapId _mapId = MapId.Nullspace;
    private EntityUid _mapUid = EntityUid.Invalid;
    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        Log.Info("OnCleanup");
        QueueDel(_stationGrid);
        _stationGrid = EntityUid.Invalid;
        if (_mapManager.MapExists(_mapId))
            _mapManager.DeleteMap(_mapId);
        _mapId = MapId.Nullspace;
        _mapUid = EntityUid.Invalid;
    }
    public void EnsurePirateSector(Entity<PirateSectorComponent> source, bool force = false)
    {
        if (!_asteroidSectorEnabled)
        {
            return;
        }
        Log.Info("EnsurePirateSector");
        if (!source.Comp.Enabled)
        {
            Log.Info("Инициализация сектора отключена (Enabled = false)");
            return;
        }
        if (_stationGrid.IsValid())
        {
            return;
        }
        Log.Info("Start load Pirate Sector");
        if (_mapId == MapId.Nullspace)
        {
            _mapUid = _mapSystem.CreateMap(out _mapId, false);
        }

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        _stationGrid = _gameTicker.MergeGameMap(_prototypeManager.Index(source.Comp.Station), _mapId, opts).FirstOrNull(HasComp<BecomesStationComponent>)!.Value;
        _metaDataSystem.SetEntityName(_mapUid, "Сектор Пиратов");
        EnsureComp<SectorAtmosSupportComponent>(_mapUid);

        var parallaxes = new[]
        {
        "CoreStation",
        "Tortuga"
        };
        var parallax = EnsureComp<ParallaxComponent>(_mapUid);
        parallax.Parallax = _random.Pick(parallaxes);

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
    }
}

