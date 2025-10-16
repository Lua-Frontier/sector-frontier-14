using Content.Server.Shuttles.Systems;
using Content.Shared._Lua.FtlPoints;
using Content.Shared._Lua.FtlPoints.Components;
using Content.Server.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using System.Numerics;
using Content.Shared.Dataset;
using Robust.Shared.Configuration;
using Content.Shared.Lua.CLVar;

namespace Content.Server._Lua.FTLPoints.Systems
{
    public sealed class SimpleStarmapSystem : EntitySystem
    {
        [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
        [Dependency] private readonly MapSystem _mapSystem = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        private ISawmill _sawmill = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<StarMapComponent, ComponentStartup>(OnStarMapStartup);
            SubscribeLocalEvent<StarmapConsoleComponent, WarpToStarMessage>(OnWarpToStar);
            _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("system.simple_starmap");
        }

        private void OnStarMapStartup(EntityUid uid, StarMapComponent component, ComponentStartup args)
        {
            if (HasComp<StarmapConsoleComponent>(uid))
            {
                _sawmill.Info($"Console StarMapComponent started up for {uid}");
                var starMapQuery = EntityQuery<StarMapComponent>();
                _sawmill.Info($"Found {starMapQuery.Count()} StarMapComponents in total");
                if (starMapQuery.Any())
                {
                    var globalStarMap = starMapQuery.First();
                    _sawmill.Info($"Global StarMapComponent has {globalStarMap.StarMap.Count} stars");
                    component.StarMap.Clear();
                    foreach (var star in globalStarMap.StarMap)
                    { component.StarMap.Add(star); }
                    _sawmill.Info($"Initialized console StarMap with {component.StarMap.Count} stars from global starmap");
                }
                else
                { _sawmill.Warning("No global StarMapComponent found. Console will not display stars."); }
            }
            else
            { _sawmill.Info($"Global StarMapComponent started up for {uid}, will generate initial sector"); }
        }

        public void GenerateInitialSector(EntityUid uid, StarMapComponent component)
        {
            _sawmill.Info($"Generating initial sector for {uid}");
            var centerStation = EntityManager.SpawnEntity("ComputerStarmap", new MapCoordinates(Vector2.Zero, Transform(uid).MapID));
            _sawmill.Info($"Spawned center station: {centerStation}");
            var minStars = _configurationManager.GetCVar(CLVars.StarmapMinStars);
            var maxStars = _configurationManager.GetCVar(CLVars.StarmapMaxStars);
            if (minStars > maxStars)
            {
                var temp = minStars;
                minStars = maxStars;
                maxStars = temp;
                _sawmill.Warning($"Swapped min and max stars: min={minStars}, max={maxStars}");
            }
            var starCount = _random.Next(minStars, maxStars + 1);
            _sawmill.Info($"Generating {starCount} stars (config: min={minStars}, max={maxStars})");
            for (int i = 0; i < starCount; i++)
            {
                var starName = GenerateRandomStarName();
                var starType = GetRandomStarType();
                var coordinates = GenerateRandomCoordinates(Transform(uid).MapID);
                var star = GenerateRandomStar(starName, starType, coordinates);
                component.StarMap.Add(star);
                _sawmill.Info($"Generated star: {starName} of type {starType} at {coordinates}");
            }
            _sawmill.Info($"Generated {component.StarMap.Count} stars for sector {uid}");
        }

        private string GetRandomStarType()
        {
            var starTypes = new[] { "StarPoint", "PlanetPoint", "AsteroidPoint", "RuinPoint", "WarpPoint" };
            return starTypes[_random.Next(starTypes.Length)];
        }

        private MapCoordinates GenerateRandomCoordinates(MapId mapId)
        {
            var radius = _random.Next(5, 15);
            var angle = _random.NextDouble() * 2 * Math.PI;
            var x = (float)(radius * Math.Cos(angle));
            var y = (float)(radius * Math.Sin(angle));
            return new MapCoordinates(new Vector2(x, y), mapId);
        }

        public Star GenerateRandomStar(string starName, string starType, MapCoordinates coordinates)
        {
            _mapSystem.CreateMap(out var mapId);
            var star = new Star(coordinates.Position, mapId, starName, coordinates.Position);
            ApplyStarEffects(mapId, starType);
            return star;
        }

        private void ApplyStarEffects(MapId mapId, string starType)
        { _sawmill.Info($"Applied effects for star type: {starType} on map {mapId}"); }

        public Star? GetStarByName(StarMapComponent component, string starName)
        { return component.StarMap.FirstOrDefault(s => s.Name == starName); }

        public void WarpToStar(EntityUid consoleUid, Star star)
        {
            _sawmill.Info($"Warping to star: {star.Name} at {star.Position}");
            if (!TryComp<TransformComponent>(consoleUid, out var consoleTransform))
            { _sawmill.Warning("Console has no TransformComponent"); return; }

            var shuttleUid = consoleTransform.GridUid;
            if (shuttleUid == null)
            { _sawmill.Warning("Console is not on a grid"); return; }
            _sawmill.Info($"Found shuttle through console: {shuttleUid}");
            if (!TryComp<ShuttleComponent>(shuttleUid.Value, out var shuttleComponent))
            { _sawmill.Warning("Shuttle component not found"); return; }
            if (shuttleComponent.Enabled == false)
            { _sawmill.Warning("Shuttle is disabled"); return; }
            var mapUid = _mapManager.GetMapEntityId(star.Map);
            var targetCoordinates = new EntityCoordinates(mapUid, star.Position);
            _shuttleSystem.FTLToCoordinates(shuttleUid.Value, shuttleComponent, targetCoordinates, Angle.Zero);
            _sawmill.Info($"Initiating FTL to {star.Name}");
        }

        public void GenerateNewSector(EntityUid uid, StarMapComponent component, Star star)
        {
            _sawmill.Info($"Generating new sector from star {star.Name}");
            var newStarCount = _random.Next(2, 5);
            for (int i = 0; i < newStarCount; i++)
            {
                var starName = GenerateRandomStarName();
                var starType = GetRandomStarType();
                var coordinates = GenerateRandomCoordinates(Transform(uid).MapID);
                var newStar = GenerateRandomStar(starName, starType, coordinates);
                component.StarMap.Add(newStar);
                _sawmill.Info($"Generated new star: {starName} of type {starType} at {coordinates}");
            }
        }

        private string GenerateRandomStarName()
        {
            string name = "Star";
            if (_prototypeManager.TryIndex<LocalizedDatasetPrototype>("NamesBorer", out var borer))
            {
                var baseLocId = _random.Pick(borer.Values);
                name = Loc.GetString(baseLocId);
            }
            if (_prototypeManager.TryIndex<LocalizedDatasetPrototype>("NamesSyndicateElite", out var elite) && _random.Prob(0.5f))
            {
                var suffixLocId = _random.Pick(elite.Values);
                var suffix = Loc.GetString(suffixLocId);
                name = $"{name} {suffix}";
            }
            return name;
        }

        private void OnWarpToStar(EntityUid uid, StarmapConsoleComponent component, WarpToStarMessage args)
        {
            var starMapQuery = EntityQuery<StarMapComponent>();
            if (!starMapQuery.Any())
            { _sawmill.Warning("No global StarMapComponent found for warp request"); return; }
            var globalStarMap = starMapQuery.First();
            var star = GetStarByName(globalStarMap, args.Star.Name);
            if (star.HasValue)
            { WarpToStar(uid, star.Value); }
            else
            { _sawmill.Warning($"Star {args.Star.Name} not found in global starmap"); }
        }
    }
}
