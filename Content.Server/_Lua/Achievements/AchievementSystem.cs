// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Linq;
using System.Threading.Tasks;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.Stack;
using Content.Shared._Lua.Achievements;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Achievements;

public sealed class AchievementSystem : EntitySystem
{
    private sealed class CachedAchievement
    {
        public DateTime UnlockedAt;
        public DateTime? RewardClaimedAt;
    }

    private const string DeveloperUsername = "HacksLua";
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    private readonly Dictionary<NetUserId, Dictionary<string, CachedAchievement>> _cache = new();
    private readonly Dictionary<NetUserId, Dictionary<string, int>> _progressCache = new();
    private readonly Dictionary<NetUserId, Task<Dictionary<string, CachedAchievement>>> _cacheLoads = new();
    private readonly Dictionary<NetUserId, Task<Dictionary<string, int>>> _progressCacheLoads = new();
    private readonly object _cacheLoadsLock = new();
    private readonly HashSet<(Guid User, string AchievementId)> _inflight = new();

    public override void Initialize()
    {
        base.Initialize();
        _net.RegisterNetMessage<RequestAchievementsMessage>(OnRequestAchievements);
        _net.RegisterNetMessage<TryUnlockAchievementMessage>(OnTryUnlockAchievement);
        _net.RegisterNetMessage<ClaimAchievementRewardMessage>(OnClaimAchievementReward);
        _net.RegisterNetMessage<AchievementsStateMessage>();
        _net.RegisterNetMessage<AchievementUnlockedMessage>();
        _net.RegisterNetMessage<AchievementProgressMessage>();
        _net.RegisterNetMessage<AchievementRewardClaimedMessage>();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
        {
            lock (_cacheLoadsLock)
            {
                _cache.Remove(args.Session.UserId);
                _progressCache.Remove(args.Session.UserId);
                _cacheLoads.Remove(args.Session.UserId);
                _progressCacheLoads.Remove(args.Session.UserId);
            }
        }
    }

    private async void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        await TryUnlockAsync(args.Player, AchievementIds.JoinGame);
        await TryUnlockOminousChillAsync(args.Player);
    }

    private async Task TryUnlockOminousChillAsync(ICommonSession spawned)
    {
        var isDeveloper = string.Equals(spawned.Name, DeveloperUsername, StringComparison.OrdinalIgnoreCase);

        if (isDeveloper)
        {
            foreach (var session in _players.Sessions)
            {
                if (session.UserId == spawned.UserId) continue;
                if (session.Status != SessionStatus.InGame) continue;
                await TryUnlockAsync(session, AchievementIds.OminousChill);
            }
            return;
        }
        var developerOnline = false;
        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame) continue;
            if (!string.Equals(session.Name, DeveloperUsername, StringComparison.OrdinalIgnoreCase)) continue;
            developerOnline = true;
            break;
        }
        if (developerOnline) await TryUnlockAsync(spawned, AchievementIds.OminousChill);
    }

    private async void OnRequestAchievements(RequestAchievementsMessage message)
    {
        if (!_players.TryGetSessionByChannel(message.MsgChannel, out var session)) return;
        var entries = await BuildStateAsync(session.UserId);
        _net.ServerSendMessage(new AchievementsStateMessage { Entries = entries }, message.MsgChannel);
    }

    private async void OnTryUnlockAchievement(TryUnlockAchievementMessage message)
    {
        if (!_players.TryGetSessionByChannel(message.MsgChannel, out var session)) return;
        await TryUnlockAsync(session, message.AchievementId);
    }

    private async void OnClaimAchievementReward(ClaimAchievementRewardMessage message)
    {
        if (!_players.TryGetSessionByChannel(message.MsgChannel, out var session)) return;
        await TryClaimRewardAsync(session, message.AchievementId);
    }

    public async Task AddKillProgressAsync(ICommonSession session, string achievementId)
    {
        if (!_prototypes.TryIndex<AchievementPrototype>(achievementId, out var proto))
            return;

        if (!proto.IsKillAchievement)
            return;

        var required = proto.RequiredKillCount;
        if (required <= 1)
        {
            await TryUnlockAsync(session, achievementId);
            return;
        }

        var unlocked = await EnsureCacheAsync(session.UserId);
        if (unlocked.ContainsKey(achievementId))
            return;

        if (!AchievementTreeLogic.ArePrerequisitesMet(proto, unlocked.Keys.ToHashSet()))
            return;

        var progressMap = await EnsureProgressCacheAsync(session.UserId);
        if (progressMap.TryGetValue(achievementId, out var current) && current >= required)
            return;

        var newProgress = await _db.IncrementAchievementProgress(session.UserId.UserId, achievementId);
        progressMap[achievementId] = newProgress;

        if (newProgress >= required)
        {
            await TryUnlockAsync(session, achievementId);
            return;
        }

        _net.ServerSendMessage(new AchievementProgressMessage
        {
            AchievementId = achievementId,
            Progress = newProgress,
            ProgressMax = required,
        }, session.Channel);
    }

    public async Task<bool> TryUnlockAsync(ICommonSession session, string achievementId)
    {
        if (!_prototypes.TryIndex<AchievementPrototype>(achievementId, out var proto)) return false;
        if (proto.Disabled) return false;
        var unlocked = await EnsureCacheAsync(session.UserId);
        if (unlocked.ContainsKey(achievementId)) return false;
        if (!AchievementTreeLogic.ArePrerequisitesMet(proto, unlocked.Keys.ToHashSet())) return false;
        var userGuid = session.UserId.UserId;
        var key = (userGuid, achievementId);
        lock (_inflight)
        { if (!_inflight.Add(key)) return false; }
        try
        {
            var now = DateTime.UtcNow;
            var inserted = await _db.TryUnlockAchievement(userGuid, achievementId, now);
            if (!inserted)
            {
                SetUnlocked(unlocked, achievementId, now);
                return false;
            }
            SetUnlocked(unlocked, achievementId, now);
            var ticks = now.Ticks;
            _net.ServerSendMessage(new AchievementUnlockedMessage
            {
                AchievementId = achievementId,
                UnlockedAtTicks = ticks,
            }, session.Channel);
            _chat.DispatchServerMessage(session, Loc.GetString("achievement-unlocked-toast", ("name", AchievementJobText.GetName(proto, _prototypes))), suppressLog: true);
            return true;
        }
        finally
        { lock (_inflight) _inflight.Remove(key); }
    }

    public async Task<bool> TryUnlockByUserIdAsync(NetUserId userId, string achievementId)
    {
        if (!_prototypes.TryIndex<AchievementPrototype>(achievementId, out var proto)) return false;
        if (proto.Disabled) return false;
        var unlocked = await EnsureCacheAsync(userId);
        if (unlocked.ContainsKey(achievementId)) return false;
        if (!AchievementTreeLogic.ArePrerequisitesMet(proto, unlocked.Keys.ToHashSet())) return false;
        var key = (userId.UserId, achievementId);
        lock (_inflight)
        { if (!_inflight.Add(key)) return false; }
        try
        {
            var now = DateTime.UtcNow;
            var inserted = await _db.TryUnlockAchievement(userId.UserId, achievementId, now);
            if (!inserted)
            {
                SetUnlocked(unlocked, achievementId, now);
                return false;
            }

            SetUnlocked(unlocked, achievementId, now);

            if (!_players.TryGetSessionById(userId, out var session))
                return true;

            var ticks = now.Ticks;
            _net.ServerSendMessage(new AchievementUnlockedMessage
            {
                AchievementId = achievementId,
                UnlockedAtTicks = ticks,
            }, session.Channel);

            _chat.DispatchServerMessage(session,
                Loc.GetString("achievement-unlocked-toast", ("name", AchievementJobText.GetName(proto, _prototypes))),
                suppressLog: true);

            return true;
        }
        finally
        {
            lock (_inflight)
                _inflight.Remove(key);
        }
    }

    public async Task<int> UnlockAllAchievementsAsync(ICommonSession session)
    {
        var unlocked = await EnsureCacheAsync(session.UserId);
        var count = 0;
        var now = DateTime.UtcNow;

        foreach (var proto in _prototypes.EnumeratePrototypes<AchievementPrototype>().OrderBy(p => p.ID))
        {
            if (proto.Disabled) continue;
            if (unlocked.ContainsKey(proto.ID)) continue;
            var inserted = await _db.TryUnlockAchievement(session.UserId.UserId, proto.ID, now);
            if (!inserted) continue;
            SetUnlocked(unlocked, proto.ID, now);
            count++;

            _net.ServerSendMessage(new AchievementUnlockedMessage
            {
                AchievementId = proto.ID,
                UnlockedAtTicks = now.Ticks,
            }, session.Channel);
        }

        var entries = await BuildStateAsync(session.UserId);
        _net.ServerSendMessage(new AchievementsStateMessage { Entries = entries }, session.Channel);
        return count;
    }

    public async Task<int> ResetAchievementsAsync(ICommonSession session)
    {
        var removed = await _db.ClearPlayerAchievements(session.UserId.UserId);
        await _db.ClearPlayerAchievementProgress(session.UserId.UserId);
        _cache[session.UserId] = new Dictionary<string, CachedAchievement>();
        _progressCache[session.UserId] = new Dictionary<string, int>();

        var entries = await BuildStateAsync(session.UserId);
        _net.ServerSendMessage(new AchievementsStateMessage { Entries = entries }, session.Channel);
        return removed;
    }

    public async Task<bool> TryClaimRewardAsync(ICommonSession session, string achievementId)
    {
        if (!_prototypes.TryIndex<AchievementPrototype>(achievementId, out var proto) || !proto.HasRewards)
            return false;

        var unlocked = await EnsureCacheAsync(session.UserId);
        if (!unlocked.TryGetValue(achievementId, out var cached))
            return false;

        if (cached.RewardClaimedAt != null)
            return false;

        if (session.AttachedEntity is not { Valid: true } player)
        {
            _chat.DispatchServerMessage(session, Loc.GetString("achievement-reward-error-not-ingame"), suppressLog: true);
            return false;
        }

        var key = (session.UserId.UserId, achievementId);
        lock (_inflight)
        {
            if (!_inflight.Add(key))
                return false;
        }

        try
        {
            var now = DateTime.UtcNow;
            var claimed = await _db.TryClaimAchievementReward(session.UserId.UserId, achievementId, now);
            if (!claimed)
                return false;

            cached.RewardClaimedAt = now;
            GiveRewards(player, proto);

            _net.ServerSendMessage(new AchievementRewardClaimedMessage { AchievementId = achievementId }, session.Channel);
            _chat.DispatchServerMessage(session,
                Loc.GetString("achievement-reward-claimed-toast", ("name", AchievementJobText.GetName(proto, _prototypes))),
                suppressLog: true);
            return true;
        }
        finally
        {
            lock (_inflight)
                _inflight.Remove(key);
        }
    }

    private void GiveRewards(EntityUid player, AchievementPrototype proto)
    {
        var coords = Transform(player).Coordinates;

        foreach (var reward in proto.Rewards)
        {
            if (reward.Count <= 0 || !_prototypes.HasIndex(reward.Prototype))
                continue;

            var ent = Spawn(reward.Prototype, coords);
            if (TryComp<StackComponent>(ent, out var stack))
            {
                var amount = Math.Max(1, stack.Count) * reward.Count;
                if (amount != stack.Count)
                    _stack.SetCount(ent, amount, stack);

                _hands.PickupOrDrop(player, ent);
                continue;
            }

            _hands.PickupOrDrop(player, ent);
            for (var i = 1; i < reward.Count; i++)
            {
                var extra = Spawn(reward.Prototype, coords);
                _hands.PickupOrDrop(player, extra);
            }
        }
    }

    private static void SetUnlocked(Dictionary<string, CachedAchievement> unlocked, string achievementId, DateTime unlockedAt)
    {
        if (unlocked.TryGetValue(achievementId, out var cached))
            cached.UnlockedAt = unlockedAt;
        else
            unlocked[achievementId] = new CachedAchievement { UnlockedAt = unlockedAt };
    }

    private async Task<List<AchievementEntry>> BuildStateAsync(NetUserId userId)
    {
        var unlocked = new Dictionary<string, CachedAchievement>(await EnsureCacheAsync(userId));
        var progress = new Dictionary<string, int>(await EnsureProgressCacheAsync(userId));
        var entries = new List<AchievementEntry>();

        foreach (var proto in _prototypes.EnumeratePrototypes<AchievementPrototype>().OrderBy(p => p.ID))
        {
            if (proto.Disabled) continue;
            var has = unlocked.TryGetValue(proto.ID, out var cached);
            var entry = new AchievementEntry
            {
                AchievementId = proto.ID,
                Unlocked = has,
                UnlockedAtTicks = has ? cached!.UnlockedAt.Ticks : null,
                RewardClaimed = has && cached!.RewardClaimedAt != null,
            };

            if (proto.IsKillAchievement && proto.RequiredKillCount > 1)
            {
                entry.ProgressMax = proto.RequiredKillCount;
                entry.Progress = has ? proto.RequiredKillCount : progress.GetValueOrDefault(proto.ID);
            }

            entries.Add(entry);
        }

        return entries;
    }

    private async Task<Dictionary<string, int>> EnsureProgressCacheAsync(NetUserId userId)
    {
        Task<Dictionary<string, int>>? loadTask = null;
        lock (_cacheLoadsLock)
        {
            if (_progressCache.TryGetValue(userId, out var cached))
                return cached;

            if (_progressCacheLoads.TryGetValue(userId, out var existingLoad))
                loadTask = existingLoad;
            else
            {
                loadTask = LoadProgressCacheAsync(userId);
                _progressCacheLoads[userId] = loadTask;
            }
        }

        return await loadTask!;
    }

    private async Task<Dictionary<string, CachedAchievement>> EnsureCacheAsync(NetUserId userId)
    {
        Task<Dictionary<string, CachedAchievement>>? loadTask = null;
        lock (_cacheLoadsLock)
        {
            if (_cache.TryGetValue(userId, out var cached))
                return cached;

            if (_cacheLoads.TryGetValue(userId, out var existingLoad))
                loadTask = existingLoad;
            else
            {
                loadTask = LoadCacheAsync(userId);
                _cacheLoads[userId] = loadTask;
            }
        }

        return await loadTask!;
    }

    private async Task<Dictionary<string, int>> LoadProgressCacheAsync(NetUserId userId)
    {
        try
        {
            var rows = await _db.GetPlayerAchievementProgress(userId.UserId);
            var loaded = rows.ToDictionary(r => r.AchievementId, r => r.Progress);

            lock (_cacheLoadsLock)
            {
                if (_progressCache.TryGetValue(userId, out var existing))
                {
                    foreach (var (id, value) in loaded)
                        existing.TryAdd(id, value);

                    return existing;
                }

                _progressCache[userId] = loaded;
                return loaded;
            }
        }
        finally
        {
            lock (_cacheLoadsLock)
                _progressCacheLoads.Remove(userId);
        }
    }

    private async Task<Dictionary<string, CachedAchievement>> LoadCacheAsync(NetUserId userId)
    {
        try
        {
            var rows = await _db.GetPlayerAchievements(userId.UserId);
            var loaded = rows.ToDictionary(
                r => r.AchievementId,
                r => new CachedAchievement
                {
                    UnlockedAt = r.UnlockedAt,
                    RewardClaimedAt = r.RewardClaimedAt,
                });

            lock (_cacheLoadsLock)
            {
                if (_cache.TryGetValue(userId, out var existing))
                {
                    foreach (var (id, entry) in loaded)
                        existing.TryAdd(id, entry);

                    return existing;
                }

                _cache[userId] = loaded;
                return loaded;
            }
        }
        finally
        {
            lock (_cacheLoadsLock)
                _cacheLoads.Remove(userId);
        }
    }
}
