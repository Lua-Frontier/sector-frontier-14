using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Reputation;

[Serializable, NetSerializable]
public sealed record ReputationTargetSummary(
    ReputationTargetKind Kind,
    NetUserId UserId,
    string Name,
    int Score,
    int ActiveVotes);

[Serializable, NetSerializable]
public sealed record ReputationVoteDetails(
    int Id,
    ReputationTargetKind Kind,
    NetUserId TargetUserId,
    string TargetName,
    NetUserId VoterUserId,
    string VoterName,
    ReputationVoteValue Value,
    string? Comment,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool Deleted,
    NetUserId? DeletedBy,
    DateTime? DeletedAt,
    string? DeleteReason);

[Serializable, NetSerializable]
public sealed class SubmitPlayerReputationVoteEvent : EntityEventArgs
{
    public NetEntity Target { get; }
    public ReputationVoteValue Value { get; }
    public string? Comment { get; }

    public SubmitPlayerReputationVoteEvent(NetEntity target, ReputationVoteValue value, string? comment)
    {
        Target = target;
        Value = value;
        Comment = comment;
    }
}

[Serializable, NetSerializable]
public sealed class ReputationModerationEuiState : EuiStateBase
{
    public ReputationTargetSummary Summary { get; }
    public List<ReputationVoteDetails> Votes { get; }

    public ReputationModerationEuiState(ReputationTargetSummary summary, List<ReputationVoteDetails> votes)
    {
        Summary = summary;
        Votes = votes;
    }
}

[Serializable, NetSerializable]
public sealed class ReputationModerationDeleteVoteMessage : EuiMessageBase
{
    public int VoteId { get; }
    public string Reason { get; }

    public ReputationModerationDeleteVoteMessage(int voteId, string reason)
    {
        VoteId = voteId;
        Reason = reason;
    }
}