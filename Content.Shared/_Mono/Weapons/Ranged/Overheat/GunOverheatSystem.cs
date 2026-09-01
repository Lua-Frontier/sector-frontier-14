using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Mono.Weapons.Ranged.Overheat;

public sealed partial class GunOverheatSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    private EntityQuery<GunComponent> _gunQuery;
    private readonly HashSet<Entity<GunOverheatComponent, GunComponent>> _activeGuns = [];

    public override void Initialize()
    {
        _gunQuery = GetEntityQuery<GunComponent>();
        SubscribeLocalEvent<GunOverheatComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<GunOverheatComponent, GunRefreshModifiersEvent>(OnRefresh);
    }

    public override void Update(float frameTime)
    {
        List<Entity<GunOverheatComponent, GunComponent>>? toRemove = null;
        foreach (var ent in _activeGuns)
        {
            ent.Comp1.Heat = Math.Clamp(ent.Comp1.Heat - ent.Comp1.HeatDissipation * frameTime, 0, ent.Comp1.HeatCapacity);

            if (ent.Comp1.HeatDissipation > 0 && ent.Comp1.Heat > 0
                || ent.Comp1.HeatDissipation <= 0 && ent.Comp1.Heat < ent.Comp1.HeatCapacity)
                continue;

            toRemove ??= [];
            toRemove.Add(ent);
        }

        if (toRemove == null)
            return;

        foreach (var ent in toRemove)
        {
            _activeGuns.Remove(ent);
            _gun.RefreshModifiers((ent.Owner, ent.Comp2));
        }
    }

    private void OnGunShot(Entity<GunOverheatComponent> ent, ref GunShotEvent ev)
    {
        if (!_gunQuery.TryComp(ent, out var gun))
            return;

        ent.Comp.Heat = Math.Clamp(ent.Comp.Heat + ent.Comp.HeatPerShot, 0, ent.Comp.HeatCapacity);

        _activeGuns.Add((ent.Owner, ent.Comp, gun));
        _gun.RefreshModifiers((ent, gun));
    }

    private void OnRefresh(Entity<GunOverheatComponent> ent, ref GunRefreshModifiersEvent ev)
    {
        var spreadPenalty = CalculatePenalty(ent.Comp.SpreadPenalty, ent.Comp);

        ev.MaxAngle *= spreadPenalty;
        ev.MinAngle *= spreadPenalty;

        ev.FireRate /= CalculatePenalty(ent.Comp.FireRatePenalty, ent.Comp);
    }

    public float CalculatePenalty(float penalty, GunOverheatComponent overheat)
    {
        var i = MathF.Pow(overheat.Heat / overheat.HeatCapacity, overheat.PenaltyExponent);
        return float.Lerp(1, penalty, i);
    }
}
