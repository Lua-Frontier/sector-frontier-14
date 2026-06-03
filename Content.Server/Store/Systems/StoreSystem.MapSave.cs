using System;
using Content.Server.Store.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.Store.Systems;

public sealed partial class StoreSystem
{
    public void ClearStaleStoreRefundRefsForMap(EntityUid mapUid, Func<EntityUid, EntityUid, bool> isEntityOnMap)
    {
        var query = AllEntityQuery<StoreRefundComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var refund, out var xform))
        {
            if (xform.MapUid != mapUid && !isEntityOnMap(uid, mapUid))
                continue;
            if (refund.StoreEntity is not { } store)
                continue;
            if (Exists(store) && isEntityOnMap(store, mapUid))
                continue;
            refund.StoreEntity = null;
            Dirty(uid, refund);
        }
    }
}
