using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel.Requirements;

[Serializable, NetSerializable]
public enum ErpBodyZone : byte
{
    Chest,
    Groin,
}

[Serializable, NetSerializable]
public sealed partial class ExposedBodyPartRequirement : InvertableErpRequirement
{
    [DataField(required: true)]
    public ErpBodyZone Part;

    public override bool IsAvailable(EntityUid uid, IEntityManager entityManager)
    {
        var coveringSlots = Part switch
        {
            ErpBodyZone.Chest => SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING | SlotFlags.UNDERWEART,
            ErpBodyZone.Groin => SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING | SlotFlags.UNDERWEARB,
            _ => SlotFlags.NONE,
        };

        if (coveringSlots == SlotFlags.NONE)
            return false;

        var inventory = entityManager.System<InventorySystem>();
        if (!inventory.TryGetContainerSlotEnumerator(uid, out var slots, coveringSlots))
            return false;

        var isExposed = !slots.NextItem(out _);

        return Inverted ? !isExposed : isExposed;
    }
}
