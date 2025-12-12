using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.VendingMachines;

public sealed class VendingOptionalInventoryValidator : ITypeValidator<Dictionary<string, uint>, MappingDataNode>
{
    public ValidationNode Validate(ISerializationManager serializationManager, MappingDataNode node, IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        var protoMan = dependencies.Resolve<IPrototypeManager>();
        var mapping = new Dictionary<ValidationNode, ValidationNode>();
        foreach (var (keyToken, valueNode) in node.Children)
        {
            var keyNode = node.GetKeyNode(keyToken);
            if (keyNode is not ValueDataNode keyValueNode) continue;
            var id = keyValueNode.Value;
            if (protoMan.HasIndex<EntityPrototype>(id)) continue;
            if (protoMan.HasIndex<OptionalEntityPrototype>(id)) continue;
            mapping.Add(new ValidatedValueNode(keyNode), new ErrorNode(valueNode, $"Неизвестный прототип '{id}' в торговом автомате и не найден optionalEntityPrototype."));
        }
        return new ValidatedMappingNode(mapping);
    }
}

