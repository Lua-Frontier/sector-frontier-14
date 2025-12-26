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
    public const double MinModPrice = 0.01;
    public const double MaxModPrice = 1.99;
    private readonly Dictionary<string, double> _modCache = new(capacity: 2048);
    private bool _loaded;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("dynamic-market");
        _ = LoadCache();
    }

    public double GetCurrentMultiplier(string prototypeId)
    {
        if (_modCache.TryGetValue(prototypeId, out var mod)) return mod;
        return 1.0;
    }

    public double GetProjectedMultiplierAfterSale(string prototypeId, int units)
    {
        if (units <= 0) return GetCurrentMultiplier(prototypeId);
        var old = GetCurrentMultiplier(prototypeId);
        return Math.Clamp(old - units * DownDeltaPerUnit, MinModPrice, MaxModPrice);
    }

    public double GetProjectedMultiplierAfterPurchase(string prototypeId, int units)
    {
        if (units <= 0) return GetCurrentMultiplier(prototypeId);
        var old = GetCurrentMultiplier(prototypeId);
        return Math.Clamp(old + units * UpDeltaPerUnit, MinModPrice, MaxModPrice);
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
            var data = await _db.GetAllDynamicMarketModPrices();
            foreach (var (pid, mod) in data)  _modCache[pid] = Math.Clamp(mod, MinModPrice, MaxModPrice);
            _loaded = true;
            _sawmill.Info($"Loaded dynamic market cache: {_modCache.Count} entries.");
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
        var updates = new List<(string protoId, double basePrice, double modPrice)>(byProto.Count);
        foreach (var (pid, agg) in byProto)
        {
            var oldMod = GetCurrentMultiplier(pid);
            var delta = agg.units * (isSale ? DownDeltaPerUnit : UpDeltaPerUnit);
            var newMod = isSale ? Math.Clamp(oldMod - delta, MinModPrice, MaxModPrice) : Math.Clamp(oldMod + delta, MinModPrice, MaxModPrice);
            var avgBase = agg.units > 0 ? (agg.weightedBaseSum / agg.units) : 0.0;
            if (avgBase < 0) avgBase = 0;
            _modCache[pid] = newMod;
            updates.Add((pid, avgBase, newMod));
        }
        try
        { await _db.UpsertDynamicMarketEntries(updates); }
        catch (Exception e)
        { _sawmill.Error($"Failed to upsert DynamicMarket entries ({updates.Count}). Exception: {e}"); }
    }
}


