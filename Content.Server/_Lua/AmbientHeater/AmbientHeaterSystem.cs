// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Atmos.EntitySystems;
using Content.Shared.Power;
using Robust.Server.GameObjects;

namespace Content.Server._Lua.AmbientHeater;

public sealed class AmbientHeaterSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly TransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AmbientHeaterComponent, PowerChangedEvent>(HandlePowerChange);
    }

    private void HandlePowerChange(EntityUid uid, AmbientHeaterComponent component, ref PowerChangedEvent args)
    {
        component.Powered = args.Powered;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AmbientHeaterComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var comp, out var xform))
        {
            if (!comp.Powered && comp.RequiresPower)
                continue;

            var grid = xform.GridUid;
            var map = xform.MapUid;
            var indices = _xform.GetGridOrMapTilePosition(ent, xform);
            var mixture = _atmosphere.GetTileMixture(grid, map, indices, true);

            if (mixture is { } && mixture.Temperature < comp.TargetTemperature)
                mixture.Temperature += comp.HeatPerSecond * frameTime;
        }
    }
}
