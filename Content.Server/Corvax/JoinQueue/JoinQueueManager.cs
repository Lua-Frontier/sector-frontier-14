using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Connection;
using Content.Server.Corvax.DiscordAuth;
using Content.Server.Sponsors;
using Content.Shared.CCVar;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Corvax.JoinQueue;
using Content.Shared._Lua.SponsorLoadout;
using Prometheus;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Corvax.JoinQueue;

/// <summary>
///     Manages new player connections when the server is full and queues them up, granting access when a slot becomes free
/// </summary>
public sealed class JoinQueueManager
{
    private static readonly Gauge QueueCount = Metrics.CreateGauge(
        "join_queue_count",
        "Amount of players in queue.");

    private static readonly Counter QueueBypassCount = Metrics.CreateCounter(
        "join_queue_bypass_count",
        "Amount of players who bypassed queue by privileges.");

    private static readonly Histogram QueueTimings = Metrics.CreateHistogram(
        "join_queue_timings",
        "Timings of players in queue",
        new HistogramConfiguration()
        {
            LabelNames = new[] {"type"},
            Buckets = Histogram.ExponentialBuckets(1, 2, 14),
        });

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConnectionManager _connectionManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly DiscordAuthManager _discordAuthManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly SponsorManager _sponsorManager = default!;

    /// <summary>
    ///     Queue of active player sessions
    /// </summary>
    private readonly List<ICommonSession> _queue = new(); // Real Queue class can't delete disconnected users
    private readonly Dictionary<NetUserId, DateTime> _reservedSlots = new();
    private readonly object _sync = new();

    private bool _isEnabled = false;

    public int PlayerInQueueCount => _queue.Count;
    public int ActualPlayersCount => _playerManager.PlayerCount - PlayerInQueueCount; // Now it's only real value with actual players count that in game

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgQueueUpdate>();

        _cfg.OnValueChanged(CCCVars.QueueEnabled, OnQueueCVarChanged, true);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        _discordAuthManager.PlayerVerified += OnPlayerVerified;
        _netManager.Connected += OnChannelConnected;
    }

    private void OnQueueCVarChanged(bool value)
    {
        _isEnabled = value;

        if (!value)
        {
            foreach (var session in _queue)
            {
                session.Channel.Disconnect("Queue was disabled");
            }
        }
    }

    private async void OnPlayerVerified(object? sender, ICommonSession session)
    {
        if (!_isEnabled)
        {
            SendToGame(session);
            return;
        }

        var isPrivilegedAdmin = await _connectionManager.HavePrivilegedJoin(session.UserId);
        var isPrivilegedDonor = _sponsorManager.TryGetActiveSponsor(session.UserId, out _);

        var isPrivileged = isPrivilegedAdmin || isPrivilegedDonor;
        var players = GetPlayersCount() - 1;
        if (players < 0) players = 0;
        var haveFreeSlot = players < _cfg.GetCVar(CCVars.SoftMaxPlayers);
        if (isPrivileged || haveFreeSlot)
        {
            SendToGame(session);

            if (isPrivileged && !haveFreeSlot)
                QueueBypassCount.Inc();

            return;
        }

        _queue.Add(session);
        ProcessQueue(false, session.ConnectedTime);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
        {
            lock (_sync)
            {
                var wasInQueue = _queue.Remove(e.Session);

                if (!wasInQueue && e.OldStatus != SessionStatus.InGame)
                    return;

                var seconds = _cfg.GetCVar(CCCVars.QueueReconnectReserveSeconds);
                if (seconds > 0 && e.OldStatus == SessionStatus.InGame && _queue.Count == 0)
                { _reservedSlots[e.Session.UserId] = DateTime.UtcNow.AddSeconds(seconds); }
                ProcessQueue(true, e.Session.ConnectedTime);

                if (wasInQueue)
                    QueueTimings.WithLabels("Unwaited").Observe((DateTime.UtcNow - e.Session.ConnectedTime).TotalSeconds);
            }
        }
    }
    private void OnChannelConnected(object? sender, NetChannelArgs args)
    {
        var userId = args.Channel.UserId;
        if (_reservedSlots.TryGetValue(userId, out var until))
        { if (DateTime.UtcNow <= until) _reservedSlots.Remove(userId); }
    }

    private int GetPlayersCount()
    {
        var count = _playerManager.PlayerCount - _queue.Count;
        var now = DateTime.UtcNow;
        var activeReserved = 0;
        if (_queue.Count == 0)
        {
            foreach (var kv in _reservedSlots.ToArray())
            {
                if (kv.Value <= now)
                { _reservedSlots.Remove(kv.Key); continue; }
                activeReserved++;
            }
        }
        if (!_cfg.GetCVar(CCVars.AdminsCountForMaxPlayers))
        { count -= _adminManager.ActiveAdmins.Count(); }
        if (count < 0) count = 0;
        return count;
    }

    /// <summary>
    ///     If possible, takes the first player in the queue and sends him into the game
    /// </summary>
    /// <param name="isDisconnect">Is method called on disconnect event</param>
    /// <param name="connectedTime">Session connected time for histogram metrics</param>
    private void ProcessQueue(bool isDisconnect, DateTime connectedTime)
    {
        var players = GetPlayersCount();
        if (isDisconnect)
            players--; // Decrease currently disconnected session but that has not yet been deleted

        var haveFreeSlot = players < _cfg.GetCVar(CCVars.SoftMaxPlayers);
        var queueContains = _queue.Count > 0;
        if (haveFreeSlot && queueContains)
        {
            var session = _queue.First();
            _queue.Remove(session);

            SendToGame(session);

            QueueTimings.WithLabels("Waited").Observe((DateTime.UtcNow - connectedTime).TotalSeconds);
        }

        SendUpdateMessages();
        QueueCount.Set(_queue.Count);
    }

    /// <summary>
    ///     Sends messages to all players in the queue with the current state of the queue
    /// </summary>
    private void SendUpdateMessages()
    {
        for (var i = 0; i < _queue.Count; i++)
        {
            _queue[i].Channel.SendMessage(new MsgQueueUpdate
            {
                Total = _queue.Count,
                Position = i + 1,
            });
        }
    }

    /// <summary>
    ///     Letting player's session into game, change player state
    /// </summary>
    /// <param name="s">Player session that will be sent to game</param>
    private void SendToGame(ICommonSession s)
    {
        Timer.Spawn(0, () => _playerManager.JoinGame(s));
    }
}
