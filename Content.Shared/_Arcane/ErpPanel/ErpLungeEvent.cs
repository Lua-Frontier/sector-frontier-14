using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Arcane.ErpPanel;

[Serializable, NetSerializable]
public sealed class ErpLungeEvent(NetEntity user, Vector2 localPos) : EntityEventArgs
{
    public readonly NetEntity User = user;

    public readonly Vector2 LocalPos = localPos;
}
