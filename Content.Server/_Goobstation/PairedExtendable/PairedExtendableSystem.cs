using System.Linq;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;

namespace Content.Server._Goobstation.PairedExtendable;

public sealed class PairedExtendableSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public bool ToggleExtendable(EntityUid user, string protoId, HandLocation side, out EntityUid? newExtendable, EntityUid? currentExtendable = null, bool makeUnremovable = true)
    {
        newExtendable = null;
        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        string? handId = null;
        if (currentExtendable != null)
        {
            foreach (var hand in _hands.EnumerateHands((user, hands)))
            {
                if (_hands.TryGetHeldItem(user, hand, out var held) && held == currentExtendable)
                {
                    handId = hand;
                    break;
                }
            }
        }

        handId ??= _hands.EnumerateHands((user, hands)).FirstOrDefault(hand => hands.Hands[hand].Location == side);
        if (handId == null)
            return false;

        if (_hands.TryGetHeldItem(user, handId, out var activeItem) && activeItem == currentExtendable)
        {
            Del(activeItem.Value);
            return true;
        }

        if (currentExtendable != null && Exists(currentExtendable.Value))
        {
            Del(currentExtendable.Value);
            return true;
        }

        newExtendable = Spawn(protoId, Transform(user).Coordinates);
        if (!_hands.TryPickup(user, newExtendable.Value, handId, handsComp: hands))
        {
            Del(newExtendable);
            newExtendable = null;
            _popup.PopupEntity(Loc.GetString("paired-extendable-hand-busy"), user, user);
            return false;
        }

        if (makeUnremovable)
            EnsureComp<UnremoveableComponent>(newExtendable.Value);

        return true;
    }
}
