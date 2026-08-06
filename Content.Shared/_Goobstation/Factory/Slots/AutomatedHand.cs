using Content.Shared.Hands.EntitySystems;

namespace Content.Shared._Goobstation.Factory.Slots;

/// <summary>
/// Abstraction over a specific hand of the machine.
/// </summary>
public sealed partial class AutomatedHand : AutomationSlot
{
    /// <summary>
    /// The name of the hand to use
    /// </summary>
    [DataField(required: true)]
    public string HandName = string.Empty;

    private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        _hands = EntMan.System<SharedHandsSystem>();
    }

    public override bool Insert(EntityUid item)
    {
        return base.Insert(item)
            && _hands.TryPickup(Owner, item, HandName);
    }

    public override bool CanInsert(EntityUid item)
    {
        return base.CanInsert(item)
            && _hands.CanPickupToHand(Owner, item, HandName);
    }

    public override EntityUid? GetItem(EntityUid? filter)
    {
        if (_hands.GetHeldItem(Owner, HandName) is not { } item
            || _filter.IsBlocked(filter, item))
            return null;

        return item;
    }
}
