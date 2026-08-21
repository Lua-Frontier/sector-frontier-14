using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnPointSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        var companyPositions = new List<EntityCoordinates>();
        var possiblePositions = new List<EntityCoordinates>();
        var playerCompany = ResolvePlayerCompany(args);

        while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                continue;

            if (spawnPoint.SpawnType == SpawnPointType.Observer)
                continue;

            if (spawnPoint.Company != null)
            {
                if (IsMatchingCompany(playerCompany, spawnPoint.Company.Value))
                    companyPositions.Add(xform.Coordinates);
                continue;
            }

            // Delta-V: Allow setting a desired SpawnPointType
            if (args.DesiredSpawnPointType != SpawnPointType.Unset)
            {
                var isMatchingJob = spawnPoint.SpawnType == SpawnPointType.Job &&
                    (args.Job == null || spawnPoint.Job == args.Job);

                switch (args.DesiredSpawnPointType)
                {
                    case SpawnPointType.Job when isMatchingJob:
                    case SpawnPointType.LateJoin when spawnPoint.SpawnType == SpawnPointType.LateJoin:
                    case SpawnPointType.Observer when spawnPoint.SpawnType == SpawnPointType.Observer:
                        possiblePositions.Add(xform.Coordinates);
                        break;
                    default:
                        continue;
                }
            }

            if (_gameTicker.RunLevel == GameRunLevel.InRound && spawnPoint.SpawnType == SpawnPointType.LateJoin)
            {
                possiblePositions.Add(xform.Coordinates);
            }

            if (_gameTicker.RunLevel != GameRunLevel.InRound &&
                spawnPoint.SpawnType == SpawnPointType.Job &&
                (args.Job == null || spawnPoint.Job == args.Job))
            {
                possiblePositions.Add(xform.Coordinates);
            }
        }

        if (companyPositions.Count > 0)
            possiblePositions = companyPositions;

        if (possiblePositions.Count == 0)
        {
            // Ok we've still not returned, but we need to put them /somewhere/.
            // TODO: Refactor gameticker spawning code so we don't have to do this!
            var points2 = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();

            if (points2.MoveNext(out var spawnPoint, out var xform))
            {
                possiblePositions.Add(xform.Coordinates);
            }
            else
            {
                Log.Error("No spawn points were available!");
                return;
            }
        }

        var spawnLoc = _random.Pick(possiblePositions);

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            spawnLoc,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station,
            session: args.Session); // Frontier
    }

    private string? ResolvePlayerCompany(PlayerSpawningEvent args)
    {
        if (args.Job != null
            && _prototypes.TryIndex(args.Job.Value, out JobPrototype? job)
            && !string.IsNullOrWhiteSpace(job.RequiredCompany))
        {
            return job.RequiredCompany
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
        }

        var profileCompany = args.HumanoidCharacterProfile?.Company;
        if (string.IsNullOrWhiteSpace(profileCompany) || profileCompany.Equals("None", StringComparison.OrdinalIgnoreCase))
            return null;

        return profileCompany;
    }

    private static bool IsMatchingCompany(string? playerCompany, ProtoId<Content.Shared._Mono.Company.CompanyPrototype> required)
    {
        return !string.IsNullOrWhiteSpace(playerCompany)
               && string.Equals(playerCompany, (string)required, StringComparison.OrdinalIgnoreCase);
    }
}
