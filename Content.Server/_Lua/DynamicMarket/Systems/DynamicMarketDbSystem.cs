// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Content.Server.Database;
using System.Threading.Tasks;

namespace Content.Server._Lua.DynamicMarket.Systems;

public sealed class DynamicMarketDbSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly ILogManager _log = default!;
    private ISawmill _sawmill = default!;
    public const double DownDeltaPerUnit = 0.0020;
    public const double UpDeltaPerUnit = 0.0012;
    private const double DriftHighTarget = 1.99;
    private const double DriftHoursToHigh = 24.0;
    private const double DriftRatePerHour = (DriftHighTarget - 1.0) / DriftHoursToHigh;
    public const double MinModPrice = 0.01;
    public const double MaxModPrice = 1.99;
    private sealed class CacheEntry
    {
        public double ModPrice = 1.0;
        public double BasePrice = 0.0;
        public long SoldUnits = 0;
        public long BoughtUnits = 0;
        public DateTime LastUpdate = DateTime.UnixEpoch;
    }
    private readonly Dictionary<string, CacheEntry> _cache = new(capacity: 2048);
    private bool _loaded;
    private static readonly TimeSpan DriftPersistInterval = TimeSpan.FromMinutes(10);
    private DateTime _nextDriftPersistUtc = DateTime.UnixEpoch;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("dynamic-market");
        _ = LoadCache();
        _nextDriftPersistUtc = DateTime.UtcNow + DriftPersistInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_loaded) return;
        var now = DateTime.UtcNow;
        if (now < _nextDriftPersistUtc) return;
        _nextDriftPersistUtc = now + DriftPersistInterval;
        _ = PersistDriftTick(now);
    }

    public double GetCurrentMultiplier(string prototypeId)
    {
        var now = DateTime.UtcNow;
        var entry = GetOrCreateEntry(prototypeId, now);
        return entry.ModPrice;
    }

    public double GetProjectedMultiplierAfterSale(string prototypeId, int units)
    {
        if (units <= 0) return GetCurrentMultiplier(prototypeId);
        var now = DateTime.UtcNow;
        var entry = GetOrCreateEntry(prototypeId, now);
        return Math.Clamp(entry.ModPrice - units * DownDeltaPerUnit, MinModPrice, MaxModPrice);
    }

    public double GetProjectedMultiplierAfterPurchase(string prototypeId, int units)
    {
        if (units <= 0) return GetCurrentMultiplier(prototypeId);
        var now = DateTime.UtcNow;
        var entry = GetOrCreateEntry(prototypeId, now);
        return Math.Clamp(entry.ModPrice + units * UpDeltaPerUnit, MinModPrice, MaxModPrice);
    }

    public void ApplySale(IReadOnlyCollection<(string prototypeId, int units, double baseUnitPrice)> sold)
    {
        if (sold.Count == 0) return;
        _ = ApplyAsync(sold, isSale: true);
    }

    public void ApplyPurchase(IReadOnlyCollection<(string prototypeId, int units, double baseUnitPrice)> bought)
    {
        if (bought.Count == 0) return;
        _ = ApplyAsync(bought, isSale: false);
    }

    private async Task LoadCache()
    {
        try
        {
            var rows = await _db.GetAllDynamicMarketEntries();
            foreach (var row in rows)
            {
                var e = new CacheEntry
                {
                    ModPrice = Math.Clamp(row.ModPrice, MinModPrice, MaxModPrice),
                    BasePrice = row.BasePrice,
                    SoldUnits = row.SoldUnits,
                    BoughtUnits = row.BoughtUnits,
                    LastUpdate = row.LastUpdate.Kind == DateTimeKind.Utc ? row.LastUpdate : DateTime.SpecifyKind(row.LastUpdate, DateTimeKind.Utc)
                };
                _cache[row.ProtoId] = e;
            }
            _loaded = true;
            _sawmill.Info($"Loaded dynamic market cache: {_cache.Count} entries.");
        }
        catch (Exception e) { _sawmill.Error($"Failed to load DynamicMarket cache from DB. Falling back to neutral prices until updates occur. Exception: {e}"); }
    }

    private async Task ApplyAsync(IReadOnlyCollection<(string prototypeId, int units, double baseUnitPrice)> batch, bool isSale)
    {
        if (!_loaded) _sawmill.Debug("DynamicMarket cache not loaded yet; applying updates with neutral baseline for missing entries.");
        var byProto = new Dictionary<string, (int units, double weightedBaseSum)>(capacity: batch.Count);
        foreach (var (pid, units, baseUnitPrice) in batch)
        {
            if (string.IsNullOrWhiteSpace(pid) || units <= 0) continue;
            if (!byProto.TryGetValue(pid, out var cur)) cur = (0, 0.0);
            cur.units += units;
            cur.weightedBaseSum += baseUnitPrice * units;
            byProto[pid] = cur;
        }
        if (byProto.Count == 0) return;
        var now = DateTime.UtcNow;
        var updates = new List<(string protoId, double basePrice, double modPrice, long soldDelta, long boughtDelta, DateTime lastUpdate)>(byProto.Count);
        foreach (var (pid, agg) in byProto)
        {
            var entry = GetOrCreateEntry(pid, now);
            ApplyDrift(entry, now);
            var delta = agg.units * (isSale ? DownDeltaPerUnit : UpDeltaPerUnit);
            var newMod = isSale ? Math.Clamp(entry.ModPrice - delta, MinModPrice, MaxModPrice) : Math.Clamp(entry.ModPrice + delta, MinModPrice, MaxModPrice);
            var avgBase = agg.units > 0 ? (agg.weightedBaseSum / agg.units) : 0.0;
            if (avgBase < 0) avgBase = 0;
            entry.ModPrice = newMod;
            entry.BasePrice = avgBase;
            entry.LastUpdate = now;
            long soldDelta = isSale ? agg.units : 0;
            long boughtDelta = isSale ? 0 : agg.units;
            entry.SoldUnits += soldDelta;
            entry.BoughtUnits += boughtDelta;
            updates.Add((pid, avgBase, newMod, soldDelta, boughtDelta, now));
        }
        try
        { await _db.UpsertDynamicMarketEntries(updates); }
        catch (Exception e)
        { _sawmill.Error($"Failed to upsert DynamicMarket entries ({updates.Count}). Exception: {e}"); }
    }

    private CacheEntry GetOrCreateEntry(string prototypeId, DateTime now)
    {
        if (_cache.TryGetValue(prototypeId, out var entry)) return entry;
        entry = new CacheEntry
        {
            ModPrice = 1.0,
            BasePrice = 0.0,
            SoldUnits = 0,
            BoughtUnits = 0,
            LastUpdate = now
        };
        _cache[prototypeId] = entry;
        return entry;
    }

    // Видит святой C# я не специально да храни тебя от лагов и багов святой C# наш...
    private static void ApplyDrift(CacheEntry entry, DateTime now)
    {
        if (entry.LastUpdate == DateTime.UnixEpoch)
        {
            entry.LastUpdate = now;
            return;
        }
        var elapsed = now - entry.LastUpdate;
        if (elapsed <= TimeSpan.Zero) return;
        var hours = elapsed.TotalHours;
        if (hours <= 0) return;
        if (entry.ModPrice < DriftHighTarget) entry.ModPrice = Math.Min(DriftHighTarget, Math.Clamp(entry.ModPrice + hours * DriftRatePerHour, MinModPrice, MaxModPrice));
        entry.LastUpdate = now;
    }

    private async Task PersistDriftTick(DateTime now)
    {
        try
        {
            foreach (var kv in _cache) ApplyDrift(kv.Value, now);
            await _db.ApplyDynamicMarketDrift(now, DriftRatePerHour, DriftHighTarget, MinModPrice);
        }
        catch (Exception e) { _sawmill.Error($"Failed to persist dynamic market drift tick. Exception: {e}"); }
    }
}


