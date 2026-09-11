using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Network;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanSpawnEntitySystem : EntitySystem
{
    [Dependency] private readonly SharedExplosionSystem _explosion = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanSpawnEntityComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [ typeof(HitscanReflectSystem) ]);
    }

    private void OnHitscanHit(Entity<HitscanSpawnEntityComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled)
            return;

        if (_net.IsClient)
            return;

        foreach (var hitEntity in args.HitEntities)
        {
            Spawn(ent.Comp.SpawnedEntity, Transform(hitEntity).Coordinates);
        }
    }
}
