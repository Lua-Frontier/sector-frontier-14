using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Lua.Shared.Reputation;

public sealed class PlayerReputationChangedEvent : EntityEventArgs
{
    public NetUserId UserId { get; }

    public PlayerReputationChangedEvent(NetUserId userId)
    {
        UserId = userId;
    }
}
