using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanStunSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanStaminaDamageComponent, HitscanRaycastFiredEvent>(OnHitscanHit, after: [ typeof(HitscanReflectSystem) ]);
    }

    private void OnHitscanHit(Entity<HitscanStaminaDamageComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled)
            return;

        foreach (var hitEntity in args.HitEntities)
        {
            _stamina.TakeStaminaDamage(hitEntity, hitscan.Comp.StaminaDamage, source: args.Shooter ?? args.Gun);
        }
    }
}
