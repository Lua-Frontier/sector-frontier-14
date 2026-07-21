using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared._Lua.Reputation;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Network;

namespace Content.Server._Lua.Reputation;

public sealed class ReputationModerationEui : BaseEui
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IServerDbManager _db = default!;

    private readonly NetUserId _targetUserId;
    private readonly string _targetName;
    private readonly ReputationTargetKind _targetKind;
    private readonly ReputationSystem _reputation;
    private ReputationModerationEuiState _state;

    public ReputationModerationEui(ReputationTargetKind targetKind, NetUserId targetUserId, string targetName)
    {
        IoCManager.InjectDependencies(this);
        _targetKind = targetKind;
        _targetUserId = targetUserId;
        _targetName = targetName;
        _reputation = _entity.System<ReputationSystem>();
        _state = new ReputationModerationEuiState(
            new ReputationTargetSummary(targetKind, targetUserId, targetName, 0, 0, 0, 0),
            new List<ReputationVoteDetails>());
    }

    public override void Opened()
    {
        base.Opened();

        if (!EnsureAuthorized())
            return;

        ReloadState();
    }

    public override EuiStateBase GetNewState()
    {
        return _state;
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!EnsureAuthorized())
            return;

        if (msg is not ReputationModerationDeleteVoteMessage delete)
            return;

        var reason = delete.Reason.Trim();
        if (reason.Length == 0 || reason.Length > ReputationConstants.MaxCommentLength)
            return;

        if (_state.Votes.All(vote => vote.Id != delete.VoteId || vote.Deleted))
            return;

        var deleted = await _db.DeleteReputationVote(delete.VoteId, Player.UserId.UserId, DateTimeOffset.UtcNow, reason);
        if (!deleted)
            return;

        _adminLog.Add(LogType.Action, $"{Player:actor} deleted reputation vote {delete.VoteId} for {_targetName:subject}: {reason}");
        await ReloadStateAsync();
    }

    private bool EnsureAuthorized()
    {
        var adminData = _admins.GetAdminData(Player);
        if (_targetKind == ReputationTargetKind.Player && adminData?.CanModeratePlayerReputation() == true)
            return true;

        if (_targetKind == ReputationTargetKind.Admin && adminData?.CanModerateAdminReputation() == true)
            return true;

        Close();
        return false;
    }

    private async void ReloadState()
    {
        await ReloadStateAsync();
    }

    private async Task ReloadStateAsync()
    {
        var summary = await _db.GetReputationSummary(_targetKind, _targetUserId.UserId);
        var votes = await _db.GetReputationVotes(_targetKind, _targetUserId.UserId, includeDeleted: true);

        _state = new ReputationModerationEuiState(
            new ReputationTargetSummary(summary.Kind, new NetUserId(summary.TargetUserId), summary.TargetName, summary.Score, summary.ActiveVotes, summary.PositiveVotes, summary.NegativeVotes),
            votes.Select(MakeVoteDetails).ToList());

        _reputation.SetCachedReputation(summary.Kind, summary.TargetUserId, new ReputationSystem.CachedReputation(summary.Score, summary.PositiveVotes, summary.NegativeVotes));
        StateDirty();
    }

    private static ReputationVoteDetails MakeVoteDetails(ReputationVoteRecord vote)
    {
        return new ReputationVoteDetails(
            vote.Id,
            vote.Kind,
            new NetUserId(vote.TargetUserId),
            vote.TargetName,
            new NetUserId(vote.VoterUserId),
            vote.VoterName,
            vote.Value,
            vote.Comment,
            vote.CreatedAt.UtcDateTime,
            vote.UpdatedAt?.UtcDateTime,
            vote.Deleted,
            vote.DeletedById is { } deletedBy ? new NetUserId(deletedBy) : null,
            vote.DeletedAt?.UtcDateTime,
            vote.DeleteReason);
    }
}