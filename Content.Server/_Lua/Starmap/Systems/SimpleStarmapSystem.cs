// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System;
using Content.Server._Lua.Starmap.Components;
using Content.Server.Backmen.Arrivals;
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
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
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
        private StarmapConfigPrototype? _cfg;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<StarMapComponent, ComponentStartup>(OnStarMapStartup);
            SubscribeLocalEvent<FTLCompletedEvent>(OnFtlCompleted);
            try { if (_prototypeManager.TryIndex<StarmapConfigPrototype>("StarmapConfig", out var c)) _cfg = c; } catch { }
        }

        private void OnStarMapStartup(EntityUid uid, StarMapComponent component, ComponentStartup args)
        { }

        public void GenerateInitialSector(EntityUid uid, StarMapComponent component)
        {
#if DEBUG
            return;
#endif
            if (_cfg == null) return;
            var randomEnabled = _configurationManager.GetCVar(CLVars.StarmapRandomGenerationEnabled);
            if (!randomEnabled)
            {
                try { EntityManager.System<StarmapSystem>().InvalidateCache(); }
                catch { } return;
            }
            var minStars = Math.Max(0, _configurationManager.GetCVar(CLVars.StarmapMinStars));
            var maxStars = Math.Max(minStars, _configurationManager.GetCVar(CLVars.StarmapMaxStars));
            if (minStars > maxStars)
            {
                var temp = minStars;
                minStars = maxStars;
                maxStars = temp;
            }
            var starCount = _random.Next(minStars, maxStars + 1);
            var minSep = MathF.Max(0f, 800f / MathF.Max(1f, _cfg.BasePixelsPerDistance));
            var existing = new List<Vector2>(component.StarMap.Count);
            foreach (var s in component.StarMap) existing.Add(s.Position);
            for (int i = 0; i < starCount; i++)
            {
                var starName = GenerateRandomStarName();
                var starType = GetRandomStarType();
                var coordinates = GenerateRandomCoordinatesSeparated(Transform(uid).MapID, existing, minSep, _cfg.StarDistanceMax);
                var star = GenerateRandomStar(starName, starType, coordinates);
                component.StarMap.Add(star);
                existing.Add(star.Position);
            }
            try { EntityManager.System<StarmapSystem>().InvalidateCache(); } catch { }
        }

        private string GetRandomStarType()
        {
            var starTypes = new[] { "StarPoint", "PlanetPoint", "AsteroidPoint", "RuinPoint", "WarpPoint" };
            return starTypes[_random.Next(starTypes.Length)];
        }

        private MapCoordinates GenerateRandomCoordinates(MapId mapId)
        {
            var maxR = MathF.Max(0f, _cfg!.StarDistanceMax);
            var r = (float)Math.Sqrt(_random.NextDouble()) * maxR;
            var angle = _random.NextDouble() * 2 * Math.PI;
            var x = (float)(r * Math.Cos(angle));
            var y = (float)(r * Math.Sin(angle));
            return new MapCoordinates(new Vector2(x, y), mapId);
        }

        private MapCoordinates GenerateRandomCoordinatesSeparated(MapId mapId, List<Vector2> existing, float minSeparation, float baseMaxRadius)
        {
            var maxRadius = MathF.Max(baseMaxRadius, minSeparation);
            for (var expand = 0; expand < 8; expand++)
            {
                for (var attempt = 0; attempt < 64; attempt++)
                {
                    var r = (float)Math.Sqrt(_random.NextDouble()) * maxRadius;
                    var angle = _random.NextDouble() * 2 * Math.PI;
                    var x = (float)(r * Math.Cos(angle));
                    var y = (float)(r * Math.Sin(angle));
                    var pos = new Vector2(x, y);
                    var ok = true;
                    for (var i = 0; i < existing.Count; i++)
                    {
                        if (Vector2.Distance(existing[i], pos) < minSeparation)
                        { ok = false; break; }
                    }
                    if (ok)
                        return new MapCoordinates(pos, mapId);
                }
                maxRadius += minSeparation * 0.5f;
            }
            return GenerateRandomCoordinates(mapId);
        }

        public Star GenerateRandomStar(string starName, string starType, MapCoordinates coordinates)
        {
            _mapSystem.CreateMap(out var mapId);
            var star = new Star(coordinates.Position, mapId, starName, coordinates.Position);
            TrySetMapEntityName(mapId, starName);
            ApplyStarEffects(mapId, starType);
            TryRenameBeaconGrid(mapId, starName);
            return star;
        }

        private void ApplyStarEffects(MapId mapId, string starType)
        {
            try
            {
                var mapUid = _mapManager.GetMapEntityId(mapId);
                string? worldgenId = null;
                if (_prototypeManager.TryIndex<DatasetPrototype>("RandomWorldgenConfigs", out var worldgenPool) && worldgenPool.Values.Count > 0)
                { worldgenId = _random.Pick(worldgenPool.Values); }
                if (!string.IsNullOrWhiteSpace(worldgenId) && _prototypeManager.TryIndex<WorldgenConfigPrototype>(worldgenId, out var wg))
                { wg.Apply(mapUid, _serializer, EntityManager); }
                if (_prototypeManager.TryIndex<DatasetPrototype>("RandomParallaxPool", out var parallaxPool) && parallaxPool.Values.Count > 0)
                {
                    var parallaxId = _random.Pick(parallaxPool.Values);
                    var parallax = EnsureComp<ParallaxComponent>(mapUid);
                    parallax.Parallax = parallaxId;
                }
                try
                {
                    var loader = EntityManager.System<Robust.Shared.EntitySerialization.Systems.MapLoaderSystem>();
                    var beaconPath = new Robust.Shared.Utility.ResPath("/Maps/_Lua/Maps/beaconstar.yml");
                    loader.TryLoadGrid(mapId, beaconPath, out _);
                }
                catch { }
            }
            catch { }
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

        private static readonly HashSet<string> MainSectorNames = new()
        {
            "Frontier Sector",
            "Asteroid Field",
            "Mercenary Sector",
            "Pirate Sector",
            "Nordfall Sector",
            "LuaTech Sector"
        };

        private void TrySetMapEntityName(MapId mapId, string name)
        {
            try
            {
                var mapUid = _mapManager.GetMapEntityId(mapId);
                var metaSys = EntityManager.System<MetaDataSystem>();
                if (!MainSectorNames.Contains(name)) metaSys.SetEntityName(mapUid, $"[STAR] {name}");
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
            var mapUid = _mapManager.GetMapEntityId(star.Map);
            if (star.Position == Vector2.Zero)
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-already-here"), consoleUid); return; }
            var currentMap = consoleTransform.MapID;
            var stars = _starmap.CollectStars();
            var isCentComTarget = _centcomm != null && _centcomm.CentComMap != MapId.Nullspace && star.Map == _centcomm.CentComMap;
            if (!isCentComTarget && !IsAdjacentByHyperlane(currentMap, star, stars))
            { PlayDenySound(consoleUid); _popup.PopupEntity(Loc.GetString("starmap-no-hyperlane"), consoleUid); return; }
            if (isCentComTarget && _centcomm != null && !_centcomm.CentComStarUnlocked && !HasComp<AllowFtlToCentComComponent>(shuttleUid.Value))
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
        private bool IsAdjacentByHyperlane(MapId currentMap, Star target, List<Star> stars)
        {
            var edges = _starmap.GetHyperlanesCached();
            var centerIndex = stars.FindIndex(s => s.Map == currentMap);
            var targetIndex = stars.FindIndex(s => s.Map == target.Map);
            if (centerIndex == -1) return true;
            if (targetIndex == -1) return false;
            foreach (var e in edges)
            { if ((e.A == centerIndex && e.B == targetIndex) || (e.B == centerIndex && e.A == targetIndex)) return true; }
            return false;
        }

        public void GenerateNewSector(EntityUid uid, StarMapComponent component, Star star)
        {
#if DEBUG
            return;
#endif
            if (!_configurationManager.GetCVar(CLVars.StarmapRandomGenerationEnabled))
                return;
            var newStarCount = _random.Next(2, 5);
            var minSep = MathF.Max(0f, 800f / MathF.Max(1f, _cfg!.BasePixelsPerDistance));
            var existing = new List<Vector2>(component.StarMap.Count);
            foreach (var s in component.StarMap) existing.Add(s.Position);
            for (int i = 0; i < newStarCount; i++)
            {
                var starName = GenerateRandomStarName();
                var starType = GetRandomStarType();
                var coordinates = GenerateRandomCoordinatesSeparated(Transform(uid).MapID, existing, minSep, _cfg.StarDistanceMax);
                var newStar = GenerateRandomStar(starName, starType, coordinates);
                component.StarMap.Add(newStar);
                existing.Add(newStar.Position);
            }
        }

        private string GenerateRandomStarName()
        {
            if (_prototypeManager.TryIndex<DatasetPrototype>("StarNames", out var starNames) && starNames.Values.Count > 0)
            { return _random.Pick(starNames.Values); }
            return "Star";
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
