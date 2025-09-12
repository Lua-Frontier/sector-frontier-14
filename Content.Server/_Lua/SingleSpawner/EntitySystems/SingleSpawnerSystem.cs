// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Spawners.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed class SingleSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SingleSpawnerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SingleSpawnerComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SingleSpawnerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (component.SpawnedEntity.HasValue && Deleted(component.SpawnedEntity.Value))
            {
                component.SpawnedEntity = null;
                TrySpawn(uid, component);
            }
        }
    }

    private void OnStartup(EntityUid uid, SingleSpawnerComponent component, ComponentStartup args)
    {
        TrySpawn(uid, component);
    }

    private void OnShutdown(EntityUid uid, SingleSpawnerComponent component, ComponentShutdown args)
    {
        if (component.SpawnedEntity.HasValue)
        {
            var spawnedEntity = component.SpawnedEntity.Value;

            if (EntityManager.EntityExists(spawnedEntity)
                && !TerminatingOrDeleted(spawnedEntity)
                && !EntityManager.IsQueuedForDeletion(spawnedEntity))
            {
                EntityManager.DeleteEntity(spawnedEntity);
            }
        }
    }

    private void TrySpawn(EntityUid uid, SingleSpawnerComponent component)
    {
        if (component.SpawnedEntity.HasValue && !Deleted(component.SpawnedEntity.Value))
            return;

        List<EntProtoId>? prototypes = null;

        if (component.SuperRarePrototypes.Count > 0 && (component.SuperRareChance == 1.0f || _random.Prob(component.SuperRareChance)))
        {
            prototypes = component.SuperRarePrototypes;
        }
        else if (component.RarePrototypes.Count > 0 && (component.RareChance == 1.0f || _random.Prob(component.RareChance)))
        {
            prototypes = component.RarePrototypes;
        }
        else if (component.CommonPrototypes.Count > 0 && (component.CommonChance == 1.0f || _random.Prob(component.CommonChance)))
        {
            prototypes = component.CommonPrototypes;
        }

        if (prototypes == null || prototypes.Count == 0)
            return;

        var entity = EntityManager.SpawnEntity(_random.Pick(prototypes), Transform(uid).Coordinates);
        component.SpawnedEntity = entity;
    }
}
