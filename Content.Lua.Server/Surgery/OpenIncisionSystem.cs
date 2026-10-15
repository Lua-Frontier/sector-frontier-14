using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Lua.Server.Surgery;

public sealed class OpenIncisionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private static readonly FixedPoint2 Damage = 0.1;
    private const string DamageType = "Slash";
    private const float MinBleed = 1.5f;

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + Interval;

        var query = EntityQueryEnumerator<IncisionOpenComponent, BodyPartComponent>();
        while (query.MoveNext(out _, out var part))
        {
            if (part.Body is not { } body || _mobState.IsDead(body))
                continue;

            var damage = new DamageSpecifier { DamageDict = { [DamageType] = Damage } };
            _damageable.TryChangeDamage(body, damage, ignoreResistances: true, canSever: false,
                targetPart: _body.GetTargetBodyPart(part));

            if (TryComp<BloodstreamComponent>(body, out var blood) && blood.BleedAmount < MinBleed)
                _bloodstream.TryModifyBleedAmount((body, blood), MinBleed - blood.BleedAmount);
        }
    }
}
