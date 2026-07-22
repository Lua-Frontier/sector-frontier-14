namespace Content.Shared.Database;

public static class ReputationConstants
{
    public const int MinScore = -1000;
    public const int MaxScore = 1000;
    public const int MinNegativeCommentLength = 120;
    public const int MaxCommentLength = 120;
    public static readonly TimeSpan VoteCooldown = TimeSpan.FromHours(1);
}

public enum ReputationTargetKind : byte
{
    Player = 0,
    Admin = 1,
}

public enum ReputationVoteValue : sbyte
{
    Dislike = -1,
    Like = 1,
}
