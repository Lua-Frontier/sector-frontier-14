using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Administration.Systems;
using Content.Server.Examine;
using Content.Server.Popups;
using Content.Shared._Lua.Reputation;
using Content.Shared.Database;
using Content.Shared.Examine;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Lua.Reputation;

public sealed class ReputationSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly ExamineSystem _examine = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public readonly record struct CachedReputation(int Score, int Positive, int Negative);

    private readonly Dictionary<(ReputationTargetKind Kind, Guid TargetUserId), CachedReputation> _scoreCache = new();
    private readonly HashSet<(ReputationTargetKind Kind, Guid TargetUserId)> _pendingScoreLoads = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, ComponentStartup>(OnActorStartup);
        SubscribeLocalEvent<ActorComponent, ExaminedEvent>(OnActorExamined);
        SubscribeNetworkEvent<SubmitPlayerReputationVoteEvent>(OnSubmitPlayerReputationVote);
    }

    private void OnActorStartup(Entity<ActorComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.PlayerSession is not { } session)
            return;

        QueueScoreLoad((ReputationTargetKind.Player, session.UserId.UserId));
    }

    private void OnActorExamined(Entity<ActorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.PlayerSession is not { } session)
            return;

        var userId = session.UserId;
        if (!TryGetCachedReputation(ReputationTargetKind.Player, userId.UserId, out var cached))
        {
            args.PushMarkup(Loc.GetString("reputation-examine-loading"));
            return;
        }

        var scoreText = $"[color=red]-{cached.Negative}[/color]/[color=green]+{cached.Positive}[/color]";
        args.PushMarkup(Loc.GetString("reputation-examine-score", ("score", scoreText)));
    }

    public int GetCachedScore(ReputationTargetKind kind, NetUserId targetUserId)
    {
        return GetCachedScore(kind, targetUserId.UserId);
    }

    public CachedReputation GetCachedReputation(ReputationTargetKind kind, NetUserId targetUserId)
    {
        return GetCachedReputation(kind, targetUserId.UserId);
    }

    public CachedReputation GetCachedReputation(ReputationTargetKind kind, Guid targetUserId)
    {
        var key = (kind, targetUserId);
        if (_scoreCache.TryGetValue(key, out var cached))
            return cached;

        QueueScoreLoad(key);
        return default;
    }

    public bool TryGetCachedScore(ReputationTargetKind kind, NetUserId targetUserId, out int score)
    {
        return TryGetCachedScore(kind, targetUserId.UserId, out score);
    }

    public bool TryGetCachedReputation(ReputationTargetKind kind, Guid targetUserId, out CachedReputation cached)
    {
        var key = (kind, targetUserId);
        if (_scoreCache.TryGetValue(key, out cached))
            return true;

        QueueScoreLoad(key);
        return false;
    }

    public bool TryGetCachedScore(ReputationTargetKind kind, Guid targetUserId, out int score)
    {
        var key = (kind, targetUserId);
        if (_scoreCache.TryGetValue(key, out var cached))
        {
            score = cached.Score;
            return true;
        }

        score = 0;
        QueueScoreLoad(key);
        return false;
    }

    public int GetCachedScore(ReputationTargetKind kind, Guid targetUserId)
    {
        var key = (kind, targetUserId);
        if (_scoreCache.TryGetValue(key, out var cached))
            return cached.Score;

        QueueScoreLoad(key);
        return 0;
    }

    public void SetCachedScore(ReputationTargetKind kind, Guid targetUserId, int score)
    {
        var existing = _scoreCache.GetValueOrDefault((kind, targetUserId));
        _scoreCache[(kind, targetUserId)] = new CachedReputation(score, existing.Positive, existing.Negative);
    }

    public void SetCachedReputation(ReputationTargetKind kind, Guid targetUserId, CachedReputation cached)
    {
        _scoreCache[(kind, targetUserId)] = cached;
    }

    private async void OnSubmitPlayerReputationVote(SubmitPlayerReputationVoteEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } user)
            return;

        try
        {
            await SubmitPlayerVote(msg, args.SenderSession, user);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to submit player reputation vote: {ex}");
            _popup.PopupEntity(Loc.GetString("reputation-popup-save-failed"), user, user);
        }
    }

    private async Task SubmitPlayerVote(SubmitPlayerReputationVoteEvent msg, ICommonSession voterSession, EntityUid user)
    {
        var target = GetEntity(msg.Target);
        if (!Exists(target) || !TryComp<ActorComponent>(target, out var targetActor))
        {
            _popup.PopupEntity(Loc.GetString("reputation-popup-player-not-found"), user, user);
            return;
        }

        if (targetActor.PlayerSession is not { } targetSession)
        {
            _popup.PopupEntity(Loc.GetString("reputation-popup-player-not-found"), user, user);
            return;
        }

        if (!_examine.IsInDetailsRange(user, target))
        {
            _popup.PopupEntity(Loc.GetString("reputation-popup-player-too-far"), user, user);
            return;
        }

        if (targetSession.UserId == voterSession.UserId)
        {
            _popup.PopupEntity(Loc.GetString("reputation-popup-self-vote"), user, user);
            return;
        }

        if (msg.Value is not (ReputationVoteValue.Like or ReputationVoteValue.Dislike))
        {
            _popup.PopupEntity(Loc.GetString("reputation-popup-invalid-value"), user, user);
            return;
        }

        var comment = string.IsNullOrWhiteSpace(msg.Comment) ? null : msg.Comment.Trim();
        if (comment?.Length > ReputationConstants.MaxCommentLength)
        {
            _popup.PopupEntity(Loc.GetString("reputation-popup-comment-too-long", ("max", ReputationConstants.MaxCommentLength)), user, user);
            return;
        }

        if (msg.Value == ReputationVoteValue.Dislike &&
            (comment == null || comment.Length < ReputationConstants.MinNegativeCommentLength))
        {
            _popup.PopupEntity(Loc.GetString("reputation-popup-negative-too-short", ("min", ReputationConstants.MinNegativeCommentLength)), user, user);
            return;
        }

        var record = await _db.TryCreateReputationVote(
            ReputationTargetKind.Player,
            targetSession.UserId.UserId,
            targetSession.Name,
            voterSession.UserId.UserId,
            voterSession.Name,
            msg.Value,
            comment,
            roundId: null,
            DateTimeOffset.UtcNow);

        if (record == null)
        {
            _popup.PopupEntity(Loc.GetString("reputation-popup-too-soon"), user, user);
            return;
        }

        var summary = await _db.GetReputationSummary(record.Kind, record.TargetUserId);
        _scoreCache[(record.Kind, record.TargetUserId)] = new CachedReputation(summary.Score, summary.PositiveVotes, summary.NegativeVotes);
        RaiseLocalEvent(new PlayerReputationChangedEvent(targetSession.UserId));

        _popup.PopupEntity(Loc.GetString("reputation-popup-saved", ("score", $"-{summary.NegativeVotes}/+{summary.PositiveVotes}")), user, user);
    }

    private void QueueScoreLoad((ReputationTargetKind Kind, Guid TargetUserId) key)
    {
        if (!_pendingScoreLoads.Add(key))
            return;

        _ = LoadScore(key);
    }

    private async Task LoadScore((ReputationTargetKind Kind, Guid TargetUserId) key)
    {
        try
        {
            var summary = await _db.GetReputationSummary(key.Kind, key.TargetUserId);
            _scoreCache[key] = new CachedReputation(summary.Score, summary.PositiveVotes, summary.NegativeVotes);

            if (key.Kind == ReputationTargetKind.Player &&
                _playerManager.TryGetSessionById(new NetUserId(key.TargetUserId), out var session))
            {
                RaiseLocalEvent(new PlayerReputationChangedEvent(session.UserId));
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load reputation score for {key.Kind}:{key.TargetUserId}: {ex}");
        }
        finally
        {
            _pendingScoreLoads.Remove(key);
        }
    }
}

public sealed class PlayerReputationChangedEvent : EntityEventArgs
{
    public NetUserId UserId { get; }

    public PlayerReputationChangedEvent(NetUserId userId)
    {
        UserId = userId;
    }
}