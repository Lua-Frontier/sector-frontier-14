
// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Explosion.Components;

namespace Content.Lua.Server.ExplodeOnInit;

public sealed class ExplodeOnInitSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ExplodeOnInitComponent, ExplosiveComponent>();

        while (query.MoveNext(out var uid, out var explodeOnInitComponent, out var explosiveComponent))
        {
            if (explodeOnInitComponent.ExplodeOnInit || explodeOnInitComponent.TimeUntilDetonation <= 0)
            {
                _explosionSystem.TriggerExplosive(uid, explosiveComponent);
            }
            else
            {
                explodeOnInitComponent.TimeUntilDetonation -= frameTime;
            }
        }
    }
}
