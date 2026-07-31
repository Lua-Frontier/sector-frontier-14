using Content.Shared._NF.Shipyard.Components;
using Robust.Shared.Player;

namespace Content.Server._NF.Shipyard.Systems;

public sealed class ShipOwnershipSystem : EntitySystem
{
    public void RegisterShipOwnership(EntityUid gridUid, ICommonSession owningPlayer)
    {
        if (!Exists(gridUid))
            return;

        var comp = EnsureComp<ShipOwnershipComponent>(gridUid);
        comp.OwnerUserId = owningPlayer.UserId;
        Dirty(gridUid, comp);

        Log.Info($"Registered ship {ToPrettyString(gridUid)} to player {owningPlayer.Name} ({owningPlayer.UserId})");
    }
}
