using System;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Damage.Systems;

public sealed class DamageOnHoldingAliveOnlySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<DamageOnHoldingAliveOnlyComponent>();
        var curTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextDamageTime == default)
            {
                comp.NextDamageTime = curTime + TimeSpan.FromSeconds(comp.Interval);
                continue;
            }

            if (curTime < comp.NextDamageTime)
                continue;

            var holder = GetHolder(uid);
            if (holder == null)
                continue;

            if (!IsAlive(holder.Value))
                continue;

            var damage = comp.Damage;
            if (comp.IgnoreResistances)
            {
                _damageable.TryChangeDamage(holder.Value, damage, true, false);
            }
            else
            {
                _damageable.TryChangeDamage(holder.Value, damage);
            }

            comp.NextDamageTime = curTime + TimeSpan.FromSeconds(comp.Interval);
        }
    }

    private EntityUid? GetHolder(EntityUid item)
    {
        var xform = Transform(item);
        var parent = xform.ParentUid;
        
        if (parent.IsValid() && HasComp<HandsComponent>(parent))
            return parent;
            
        return null;
    }

    private bool IsAlive(EntityUid entity)
    {
        if (TryComp<MobStateComponent>(entity, out var mobState))
        {
            return mobState.CurrentState != MobState.Dead;
        }
        return true;
    }
}