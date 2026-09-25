using Content.Shared.Database;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Lua.Shared.Reputation;

public readonly record struct CachedReputation(int Score, int Positive, int Negative);

public interface IReputationSystem : IEntitySystem
{
    CachedReputation GetCachedReputation(ReputationTargetKind kind, NetUserId targetUserId);
    void SetCachedReputation(ReputationTargetKind kind, Guid targetUserId, CachedReputation cached);
}
