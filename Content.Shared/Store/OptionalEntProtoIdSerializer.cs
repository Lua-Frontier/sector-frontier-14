using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.Store;

public sealed class OptionalEntProtoIdSerializer : ITypeValidator<EntProtoId, ValueDataNode>
{
    public ValidationNode Validate(ISerializationManager serializationManager, ValueDataNode node, IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        var prototypes = dependencies.Resolve<IPrototypeManager>();
        if (prototypes.TryGetKindFrom<EntityPrototype>(out _) && prototypes.HasMapping<EntityPrototype>(node.Value))
            return new ValidatedValueNode(node);
        if (prototypes.HasIndex<OptionalEntityPrototype>(node.Value))
            return new ValidatedValueNode(node);
        return new ErrorNode(node, $"Не найден {nameof(EntityPrototype)} {node.Value}");
    }
}

