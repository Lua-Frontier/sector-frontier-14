using Robust.Shared.Prototypes;

namespace Content.Shared.VendingMachines
{
    [Prototype]
    public sealed partial class VendingMachineInventoryPrototype : IPrototype
    {
        [ViewVariables]
        [IdDataField]
        public string ID { get; private set; } = default!;

        [DataField("startingInventory", customTypeSerializer:typeof(VendingOptionalInventoryValidator))]
        public Dictionary<string, uint> StartingInventory { get; private set; } = new();

        [DataField("emaggedInventory", customTypeSerializer:typeof(VendingOptionalInventoryValidator))]
        public Dictionary<string, uint>? EmaggedInventory { get; private set; }

        [DataField("contrabandInventory", customTypeSerializer:typeof(VendingOptionalInventoryValidator))]
        public Dictionary<string, uint>? ContrabandInventory { get; private set; }
    }
}
