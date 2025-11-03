using Content.Server.Backmen.Disease.Components;
using Content.Shared.Backmen.Disease;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Backmen.Disease;

public sealed class InfectOnHitSystem : EntitySystem
{
    [Dependency] private readonly DiseaseSystem _disease = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InfectOnMeleeComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<InfectOnProjectileHitComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnMeleeHit(Entity<InfectOnMeleeComponent> entity, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;
        foreach (var target in args.HitEntities)
        {
            if (!TryComp<DiseaseCarrierComponent>(target, out var carrier)) continue;
            _disease.TryInfect((target, carrier), entity.Comp.Disease, entity.Comp.InfectChance, forced: true);
        }
    }

    private void OnProjectileHit(Entity<InfectOnProjectileHitComponent> entity, ref ProjectileHitEvent args)
    {
        if (!TryComp<DiseaseCarrierComponent>(args.Target, out var carrier)) return;
        _disease.TryInfect((args.Target, carrier), entity.Comp.Disease, entity.Comp.InfectChance, forced: true);
        RemCompDeferred<InfectOnProjectileHitComponent>(entity);
    }
}

