using Content.Server.Explosion.EntitySystems;
using Content.Server.Radiation.Components;
using Content.Server.Radiation.Events;
using Content.Shared.Explosion.Components;
using Content.Shared.Radiation.Components;

namespace Content.Server._Mono.Radiation;

public sealed partial class ChainRadiationSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadiationSystemUpdatedEvent>(OnUpdate);
    }

    private void OnUpdate(RadiationSystemUpdatedEvent args)
    {
        var query = EntityQueryEnumerator<ChainRadiationComponent, RadiationReceiverComponent, RadiationSourceComponent>();
        while (query.MoveNext(out var uid, out var chain, out var receiver, out var source))
        {
            source.Intensity = chain.BaseIntensity * (1f + receiver.CurrentRadiation * chain.Coefficient);

            if (source.Intensity > chain.ExplosionThreshold)
            {
                var coord = Transform(uid).Coordinates;
                foreach (var other in _lookup.GetEntitiesInRange<ChainRadiationComponent>(coord, chain.ChainExplosionRadius))
                {
                    // teleport them to us for combined explosion
                    _transform.SetCoordinates(other, coord);

                    Explode(other);
                }

                Explode((uid, chain));
            }
        }
    }

    private void Explode(Entity<ChainRadiationComponent> ent)
    {
        if (!TryComp<ExplosiveComponent>(ent, out var explosive))
            return;

        _explosion.QueueExplosion(
            ent,
            explosive.ExplosionType,
            ent.Comp.TotalIntensity,
            ent.Comp.IntensitySlope,
            ent.Comp.MaxIntensity);

        QueueDel(ent);
    }
}
