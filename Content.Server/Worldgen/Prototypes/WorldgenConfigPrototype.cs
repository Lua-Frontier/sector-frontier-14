using Content.Server.Worldgen.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Worldgen.Prototypes;

[Prototype]
public sealed partial class WorldgenConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("components", required: true)]
    public ComponentRegistry Components { get; private set; } = default!;

    public void Apply(EntityUid target, ISerializationManager serialization, IEntityManager entityManager)
    {
        ApplyComponent(target, serialization, entityManager, Components.Values);
    }

    public static void ApplyMany(
        EntityUid target,
        IEnumerable<string> configIds,
        IPrototypeManager prototypes,
        ISerializationManager serialization,
        IEntityManager entityManager)
    {
        foreach (var configId in configIds)
        {
            if (!prototypes.TryIndex<WorldgenConfigPrototype>(configId, out var wg))
                continue;

            ApplyComponent(target, serialization, entityManager, wg.Components.Values);
        }
    }

    private static void ApplyComponent(
        EntityUid target,
        ISerializationManager serialization,
        IEntityManager entityManager,
        IEnumerable<EntityPrototype.ComponentRegistryEntry> entries)
    {
        foreach (var data in entries)
        {
            var comp = (Component) serialization.CreateCopy(data.Component, notNullableOverride: true);
            var compType = comp.GetType();

            if (compType == typeof(WorldControllerComponent) && entityManager.HasComponent(target, compType))
                continue;

            if (entityManager.HasComponent(target, compType))
                continue;

            entityManager.AddComponent(target, comp);
        }
    }
}
