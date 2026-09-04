using Content.Shared._RMC14.Explosion;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Explosion;

[RegisterComponent]
public sealed partial class RMCExplosionShockWaveSpawnedComponent : Component;

public sealed class RMCExplosionShockWaveSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public void TrySpawn(EntityUid explosion, MapCoordinates epicenter, string explosionType)
    {
        if (HasComp<RMCExplosionShockWaveSpawnedComponent>(explosion))
            return;
        if (string.IsNullOrEmpty(explosionType))
            return;
        if (!_prototype.TryIndex(explosionType, out ExplosionPrototype? proto) || proto.ShockWave is not { } shockWave)
            return;
        if (!_map.MapExists(epicenter.MapId))
            return;
        var uid = Spawn(shockWave, epicenter);
        var wave = EnsureComp<RMCExplosionShockWaveComponent>(uid);
        wave.StartTime = _timing.CurTime;
        EnsureComp<RMCExplosionShockWaveSpawnedComponent>(explosion);
    }
}
