using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Components.Debris;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;

namespace Content.Server.Worldgen.Systems.Debris;

public sealed class DebrisPregenSystem : BaseWorldSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly DebrisFeaturePlacerSystem _debris = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;
    private bool _enabled;
    private float _radius;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _logManager.GetSawmill("world.debris.pregen");
        Subs.CVar(_cfg, CCVars.WorldgenDebrisPregenEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, CCVars.WorldgenDebrisPregenRadius, value => _radius = value, true);
    }

    public void InitializePlacer(EntityUid mapUid)
    {
        if (!_enabled || _radius <= 0f)
            return;

        if (!TryComp<WorldControllerComponent>(mapUid, out var controller) ||
            !TryComp<MapComponent>(mapUid, out _))
            return;

        var chunkRadius = (int) MathF.Ceiling(_radius / WorldGen.ChunkSize) + 1;
        var radiusSquared = _radius * _radius;
        var processedChunks = 0;
        var shellGrids = 0;

        for (var x = -chunkRadius; x <= chunkRadius; x++)
        {
            for (var y = -chunkRadius; y <= chunkRadius; y++)
            {
                var coords = new Vector2i(x, y);
                if (WorldGen.ChunkToWorldCoordsCentered(coords).LengthSquared() > radiusSquared)
                    continue;

                if (!_debris.ChunkIntersectsCluster(coords))
                    continue;

                var chunkUid = GetOrCreateChunk(coords, mapUid, controller);
                if (chunkUid is null)
                    continue;

                if (!TryComp<DebrisFeaturePlacerControllerComponent>(chunkUid.Value, out var placer))
                    continue;

                if (placer.Pregenerated)
                    continue;

                var before = placer.OwnedDebris.Count;
                _debris.TryPlaceDebrisForChunk(chunkUid.Value, placer, true);
                shellGrids += placer.OwnedDebris.Count - before;
                processedChunks++;
            }
        }

        _sawmill.Info($"Pregenerated {shellGrids} debris shell grids across {processedChunks} chunks on {ToPrettyString(mapUid)}.");
    }
}
