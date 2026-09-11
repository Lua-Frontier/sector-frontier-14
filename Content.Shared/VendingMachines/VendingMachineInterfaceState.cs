using Robust.Shared.Serialization;

namespace Content.Shared.VendingMachines
{
    [Serializable, NetSerializable]
    public sealed class VendingMachineEjectMessage : BoundUserInterfaceMessage
    {
        public readonly InventoryType Type;
        public readonly string ID;
        public VendingMachineEjectMessage(InventoryType type, string id)
        {
            Type = type;
            ID = id;
        }
    }

    [Serializable, NetSerializable]
    public sealed class VendingMachineBalanceMessage : BoundUserInterfaceMessage
    {
        public readonly int Balance;

        public VendingMachineBalanceMessage(int balance)
        {
            Balance = balance;
        }
    }

    [Serializable, NetSerializable]
    public sealed class VendingMachineRequestBalanceMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public enum VendingMachineUiKey
    {
        Key,
    }
}
