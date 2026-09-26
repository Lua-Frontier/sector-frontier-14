// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.SpaceHazards;

public abstract class SharedNebulaEnvironmentSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private readonly List<NebulaWeatherPrototype> _fireRateWeatherScratch = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponent, QueryFireRateMultiplierEvent>(OnFireRateQuery);
    }

    private void OnFireRateQuery(Entity<GunComponent> ent, ref QueryFireRateMultiplierEvent args)
    {
        var xform = Transform(ent.Owner);
        if (xform.GridUid is not { } gridUid)
            return;

        if (!TryFillActiveWeathers(gridUid, _fireRateWeatherScratch))
            return;

        var cooldownMultiplier = 1f;
        foreach (var weather in _fireRateWeatherScratch)
            cooldownMultiplier = MathF.Max(cooldownMultiplier, weather.WeaponCooldownMultiplier);

        if (cooldownMultiplier <= 1f)
            return;

        var resistance = HasComp<NebulaWeaponResistanceComponent>(ent.Owner)
            ? Math.Clamp(Comp<NebulaWeaponResistanceComponent>(ent.Owner).Resistance, 0f, 1f)
            : 0f;
        args.ReloadTimeMul *= float.Lerp(cooldownMultiplier, 1f, resistance);
    }

    protected bool TryFillActiveWeathers(EntityUid uid, List<NebulaWeatherPrototype> output)
    {
        output.Clear();
        if (!TryComp(uid, out NebulaPresenceComponent? presence))
            return false;

        if (presence.ActiveWeathers.Count == 0)
        {
            if (_prototypes.TryIndex(presence.Weather, out NebulaWeatherPrototype? fallback))
                output.Add(fallback);
            return output.Count > 0;
        }

        foreach (var weatherId in presence.ActiveWeathers)
        {
            if (_prototypes.TryIndex(weatherId, out NebulaWeatherPrototype? weather))
                output.Add(weather);
        }

        return output.Count > 0;
    }
}
