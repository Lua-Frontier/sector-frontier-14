using System.Linq;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnPointSystem : EntitySystem
{
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
        var jobPositions = new List<EntityCoordinates>();
        var genericLateJoinPositions = new List<EntityCoordinates>();
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

            if (spawnPoint.SpawnType != SpawnPointType.LateJoin)
                continue;

            if (args.DesiredSpawnPointType != SpawnPointType.Unset
                && args.DesiredSpawnPointType != SpawnPointType.LateJoin)
            {
                continue;
            }

            if (spawnPoint.Job != null)
            {
                if (args.Job != null && spawnPoint.Job == args.Job)
                    jobPositions.Add(xform.Coordinates);
            }
            else
            {
                genericLateJoinPositions.Add(xform.Coordinates);
            }
        }

        List<EntityCoordinates> possiblePositions;
        if (companyPositions.Count > 0)
            possiblePositions = companyPositions;
        else if (jobPositions.Count > 0)
            possiblePositions = jobPositions;
        else
            possiblePositions = genericLateJoinPositions;

        if (possiblePositions.Count == 0)
        {
            Log.Warning("No spawn points on station {Station} for job {Job}", args.Station, args.Job);
            return;
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
