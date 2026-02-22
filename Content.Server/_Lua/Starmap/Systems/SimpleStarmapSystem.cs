// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System;
using Content.Server._Lua.Sectors;
using Content.Server._Lua.Starmap.Components;
using Content.Server.Backmen.Arrivals;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Worldgen.Components.GC;
using Content.Server.Worldgen.Prototypes;
using Content.Shared._Lua.Starmap;
using Content.Shared._Lua.Starmap.Components;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.Dataset;
using Content.Shared.Lua.CLVar;
using Content.Shared.Parallax;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Content.Server._Lua.Starmap.Systems
{
    public sealed class SimpleStarmapSystem : EntitySystem
    {
        [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
        [Dependency] private readonly MapSystem _mapSystem = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly StarmapSystem _starmap = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly ISerializationManager _serializer = default!;
        [Dependency] private readonly CentcommSystem _centcomm = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly GameTicker _ticker = default!;
        [Dependency] private readonly SectorSystem _sectors = default!;
        [Dependency] private readonly SharedContainerSystem _containers = default!;
        private StarmapConfigPrototype? _cfg;

        private readonly Dictionary<MapId, StarDefinition> _pendingLazyStars = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<StarMapComponent, ComponentStartup>(OnStarMapStartup);
            SubscribeLocalEvent<FTLCompletedEvent>(OnFtlCompleted);
            try { if (_prototypeManager.TryIndex<StarmapConfigPrototype>("StarmapConfig", out var c)) _cfg = c; } catch { }
        }

        private void OnStarMapStartup(EntityUid uid, StarMapComponent component, ComponentStartup args)
        { }

        public void LoadStarsFromData(EntityUid uid, StarMapComponent component)
        {
            var dataId = _configurationManager.GetCVar(CLVars.StarmapDataId);
            if (!_prototypeManager.TryIndex<StarmapDataPrototype>(dataId, out var data))
                return;

            var lazyLoading = _configurationManager.GetCVar(CLVars.StarmapLazyLoading);

            foreach (var def in data.Stars)
            {
                if (def.StarType == "centcom" || def.StarType == "frontier" || def.StarType == "sector")
                    continue;

                var currentPreset = _ticker.CurrentPreset?.ID;
                if (def.RequiredGamePresets != null && def.RequiredGamePresets.Length > 0)
                {
                    if (currentPreset == null || !def.RequiredGamePresets.Contains(currentPreset))
                        continue;
                }
                else if (!string.IsNullOrWhiteSpace(def.RequiredGamePreset))
                {
                    if (currentPreset != def.RequiredGamePreset)
                        continue;
                }

                if (def.AutoStart || !lazyLoading)
                {
                    if (TryLoadStarMap(def, out var mapId))
                    {
                        var star = new Star(def.Position, mapId, def.Name, def.Position);
                        component.StarMap.Add(star);
                    }
                }
                else
                {
                    _mapSystem.CreateMap(out var mapId);
                    TrySetMapEntityName(mapId, def.Name);
                    var star = new Star(def.Position, mapId, def.Name, def.Position);
                    component.StarMap.Add(star);
                    _pendingLazyStars[mapId] = def;
                }
            }

            try { EntityManager.System<StarmapSystem>().InvalidateCache(); } catch { }
        }

        private bool TryLoadStarMap(StarDefinition def, out MapId mapId)
        {
            _mapSystem.CreateMap(out mapId);
            try
            {
                var mapUid = _mapManager.GetMapEntityId(mapId);

                if (!string.IsNullOrWhiteSpace(def.WorldgenConfig)
                    && _prototypeManager.TryIndex<WorldgenConfigPrototype>(def.WorldgenConfig, out var wg))
                {
                    wg.Apply(mapUid, _serializer, EntityManager);
                }

                if (def.ParallaxPool.Length > 0)
                {
                    var parallaxId = _random.Pick(def.ParallaxPool);
                    var parallax = EnsureComp<ParallaxComponent>(mapUid);
                    parallax.Parallax = parallaxId;
                }

                if (!string.IsNullOrWhiteSpace(def.Station))
                {
                    try
                    {
                        var loader = EntityManager.System<Robust.Shared.EntitySerialization.Systems.MapLoaderSystem>();
                        var mapPath = new Robust.Shared.Utility.ResPath($"/Maps/_Lua/Maps/{def.Station.ToLowerInvariant()}.yml");
                        var beaconPath = new Robust.Shared.Utility.ResPath("/Maps/_Lua/Maps/beaconstar.yml");
                        loader.TryLoadGrid(mapId, beaconPath, out _);
                    }
                    catch { }
                }

                TrySetMapEntityName(mapId, def.Name);
                TryRenameBeaconGrid(mapId, def.Name);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryLazyLoadStar(MapId mapId)
        {
            if (!_pendingLazyStars.TryGetValue(mapId, out var def))
                return false;

            _pendingLazyStars.Remove(mapId);

            try
            {
                var mapUid = _mapManager.GetMapEntityId(mapId);

                if (!string.IsNullOrWhiteSpace(def.WorldgenConfig)
                    && _prototypeManager.TryIndex<WorldgenConfigPrototype>(def.WorldgenConfig, out var wg))
                {
                    wg.Apply(mapUid, _serializer, EntityManager);
                }

                if (def.ParallaxPool.Length > 0)
                {
                    var parallaxId = _random.Pick(def.ParallaxPool);
                    var parallax = EnsureComp<ParallaxComponent>(mapUid);
                    parallax.Parallax = parallaxId;
                }

                if (!string.IsNullOrWhiteSpace(def.Station))
                {
                    try
                    {
                        var loader = EntityManager.System<Robust.Shared.EntitySerialization.Systems.MapLoaderSystem>();
                        var beaconPath = new Robust.Shared.Utility.ResPath("/Maps/_Lua/Maps/beaconstar.yml");
                        loader.TryLoadGrid(mapId, beaconPath, out _);
                    }
                    catch { }
                }

                TryRenameBeaconGrid(mapId, def.Name);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void TryRenameBeaconGrid(MapId mapId, string starName)
        {
            try
            {
                var query = AllEntityQuery<BecomesStationComponent, TransformComponent, MetaDataComponent>();
                while (query.MoveNext(out var uid, out var becomes, out var xform, out var meta))
                {
                    if (xform.MapID != mapId) continue;
                    if (!string.Equals(becomes.Id, "Beacon", StringComparison.Ordinal)) continue;
                    EntityManager.System<MetaDataSystem>().SetEntityName(uid, $"Маяк \"{starName}\"");
                    break;
                }
                var qWarp = AllEntityQuery<MetaDataComponent, TransformComponent>();
                while (qWarp.MoveNext(out var uid, out var meta, out var xform))
                {
                    if (xform.MapID != mapId) continue;
                    var pid = meta.EntityPrototype?.ID;
                    if (!string.Equals(pid, "WarpPoint", StringComparison.Ordinal)) continue;
                    EntityManager.System<MetaDataSystem>().SetEntityName(uid, $"Маяк \"{starName}\"");
                    break;
                }
            }
            catch { }
        }

        private void TrySetMapEntityName(MapId mapId, string name)
        {
            try
            {
                var mapUid = _mapManager.GetMapEntityId(mapId);
                var metaSys = EntityManager.System<MetaDataSystem>();
                metaSys.SetEntityName(mapUid, $"[STAR] {name}");
            }
            catch { }
        }

        private bool IsGcAbleGrid(EntityUid gridUid)
        {
            if (HasComp<GCAbleObjectComponent>(gridUid)) return true;
            var query = AllEntityQuery<GCAbleObjectComponent>();
            while (query.MoveNext(out var comp))
            { if (comp.LinkedGridEntity == gridUid) return true; }
            return false;
        }

        public Star? GetStarByName(StarMapComponent component, string starName)
        { return component.StarMap.FirstOrDefault(s => s.Name == starName); }

        public void WarpToStar(EntityUid consoleUid, Star star)
        {
            if (!TryComp<TransformComponent>(consoleUid, out var consoleTransform)) { return; }
            var shuttleUid = consoleTransform.GridUid;
            if (shuttleUid == null) { return; }
            if (!TryComp<ShuttleComponent>(shuttleUid.Value, out var shuttleComponent)) { return; }
            if (HasComp<WarpTransitComponent>(shuttleUid.Value))
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("shuttle-console-in-ftl"), consoleUid); return; }
            if (!_mapManager.MapExists(star.Map))
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
            TryLazyLoadStar(star.Map);
            var mapUid = _mapManager.GetMapEntityId(star.Map);
            if (star.Position == Vector2.Zero)
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-already-here"), consoleUid); return; }
            var currentMap = consoleTransform.MapID;
            var stars = _starmap.CollectStars();
            var isCentComTarget = _centcomm != null && _centcomm.CentComMap != MapId.Nullspace && star.Map == _centcomm.CentComMap;
            var isInCentCom = _centcomm != null && _centcomm.CentComMap != MapId.Nullspace && currentMap == _centcomm.CentComMap;
            if (!isCentComTarget)
            {
                if (isInCentCom)
                {
                    if (star.Map != _ticker.DefaultMap)
                    { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
                }
                else if (!IsAdjacentByHyperlane(currentMap, star, stars))
                { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
            }
            if (isCentComTarget && _centcomm != null && !_centcomm.CentComStarUnlocked && !HasComp<AllowFtlToCentComComponent>(shuttleUid.Value))
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
            if (!HasDiskForSector(consoleUid, star.Map))
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
            if (!_shuttleSystem.CanFTL(shuttleUid.Value, out var reason))
            { PlayDenySound(consoleUid); if (!string.IsNullOrEmpty(reason)) _popup.PopupEntity(reason!, consoleUid); return; }
            if (!_shuttleSystem.TryGetBluespaceDrive(shuttleUid.Value, out var warpDriveUid, out var warpDrive) || warpDriveUid == null)
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-warpdrive"), consoleUid); return; }
            if (TryComp<MapGridComponent>(shuttleUid.Value, out var grid))
            {
                var xform = Transform(shuttleUid.Value);
                var bounds = xform.WorldMatrix.TransformBox(grid.LocalAABB).Enlarged(ShuttleConsoleSystem.ShuttleFTLRange);
                var dockedShuttles = new HashSet<EntityUid>();
                _shuttleSystem.GetAllDockedShuttlesIgnoringFTLLock(shuttleUid.Value, dockedShuttles);
                foreach (var other in _mapManager.FindGridsIntersecting(xform.MapID, bounds))
                {
                    if (other.Owner == shuttleUid.Value) continue;
                    if (dockedShuttles.Contains(other.Owner)) continue;
                    if (IsGcAbleGrid(other.Owner)) continue;
                    PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("shuttle-ftl-proximity"), consoleUid); return;
                }
            }
            void PlayDenySound(EntityUid uid)
            { _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg"), uid); }
            var transit = EnsureComp<WarpTransitComponent>(shuttleUid.Value);
            transit.TargetMap = star.Map;
            var angle = (float)(_random.NextDouble() * 2 * Math.PI);
            var radius = _random.Next(1000, 5001);
            var offset = new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius);
            var targetPos = star.Position + offset;
            transit.TargetPosition = targetPos;
            Dirty(shuttleUid.Value, transit);
            var targetCoordinates = new EntityCoordinates(mapUid, targetPos);
            _shuttleSystem.FTLToCoordinates(shuttleUid.Value, shuttleComponent, targetCoordinates, Angle.Zero);
            try { EntityManager.System<StarmapSystem>().RefreshConsoles(); } catch { }
        }
        private bool HasDiskForSector(EntityUid consoleUid, MapId targetMap)
        {
            if (targetMap == _ticker.DefaultMap) return true;
            if (_centcomm != null && _centcomm.CentComMap != MapId.Nullspace && targetMap == _centcomm.CentComMap) return true;
            if (!_containers.TryGetContainer(consoleUid, "disk_slot", out var diskCont) || diskCont.ContainedEntities.Count == 0) return false;
            var disk = diskCont.ContainedEntities[0];
            if (!TryComp<StarMapCoordinatesDiskComponent>(disk, out var diskComp) || diskComp.AllowedSectorIds.Count == 0) return false;
            var currentPreset = _ticker.CurrentPreset?.ID;
            foreach (var sid in diskComp.AllowedSectorIds)
            {
                if (string.IsNullOrWhiteSpace(sid)) continue;
                MapId mapId;
                if (sid == "FrontierSector") mapId = _ticker.DefaultMap;
                else if (_sectors.TryGetMapId(sid, out var resolved)) mapId = resolved;
                else if (currentPreset == "LuaAdventure")
                {
                    var altId = sid switch { "TypanSector" => "TypanSectorLua", "PirateSector" => "PirateSectorLua", _ => null };
                    if (altId == null || !_sectors.TryGetMapId(altId, out resolved)) continue;
                    mapId = resolved;
                }
                else continue;
                if (mapId == targetMap) return true;
            }
            return false;
        }

        private bool IsAdjacentByHyperlane(MapId currentMap, Star target, List<Star> stars)
        {
            var edges = _starmap.GetHyperlanesCached();
            var centerIndex = stars.FindIndex(s => s.Map == currentMap);
            var targetIndex = stars.FindIndex(s => s.Map == target.Map);
            if (centerIndex == -1) return false;
            if (targetIndex == -1) return false;
            foreach (var e in edges)
            { if ((e.A == centerIndex && e.B == targetIndex) || (e.B == centerIndex && e.A == targetIndex)) return true; }
            return false;
        }

        private void OnFtlCompleted(ref FTLCompletedEvent ev)
        {
            var shuttle = ev.Entity;
            if (!TryComp<WarpTransitComponent>(shuttle, out var transit)) return;
            RemCompDeferred<WarpTransitComponent>(shuttle);
            var mapUid = _mapManager.GetMapEntityId(transit.TargetMap);
            var targetCoords = new EntityCoordinates(mapUid, transit.TargetPosition);
            _shuttleSystem.TryFTLProximity((shuttle, Transform(shuttle)), targetCoords);
            if (TryComp<WarpTransitComponent>(shuttle, out var arriving))
            {
                Dirty(shuttle, arriving);
                Timer.Spawn(TimeSpan.FromSeconds(2), () => { if (TryComp<WarpTransitComponent>(shuttle, out var still)) RemCompDeferred<WarpTransitComponent>(shuttle); });
            }
            try { EntityManager.System<ShuttleConsoleSystem>().RefreshShuttleConsoles(shuttle); } catch { }
            try { EntityManager.System<StarmapSystem>().RefreshConsoles(); } catch { }
        }
    }
}
