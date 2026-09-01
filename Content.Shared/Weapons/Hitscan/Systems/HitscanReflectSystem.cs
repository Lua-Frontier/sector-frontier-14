using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Reflect;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanReflectSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanReflectComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanReflectComponent> hitscan, ref HitscanRaycastFiredEvent args)
    {
        if (hitscan.Comp.ReflectiveType == ReflectType.None || args.HitEntities.Count == 0)
            return;

        if (hitscan.Comp.CurrentReflections >= hitscan.Comp.MaxReflections)
            return;

        var damage = TryComp<HitscanBasicDamageComponent>(hitscan, out var dmgComp)
            ? dmgComp.Damage * _damageable.UniversalHitscanDamageModifier
            : null;

        var ev = new HitScanReflectAttemptEvent(args.Shooter ?? args.Gun, args.Gun, hitscan.Comp.ReflectiveType, args.ShotDirection, false, damage);
        RaiseLocalEvent(args.HitEntities.First(), ref ev);

        if (!ev.Reflected)
            return;

        hitscan.Comp.CurrentReflections++;

        args.Canceled = true;

        var fromEffect = Transform(args.HitEntities.First()).Coordinates;

        var hitFiredEvent = new HitscanTraceEvent
        {
            FromCoordinates = fromEffect,
            ShotDirection = ev.Direction,
            Gun = args.Gun,
            Shooter = args.HitEntities.First(),
        };

        RaiseLocalEvent(hitscan, ref hitFiredEvent);
    }
}
