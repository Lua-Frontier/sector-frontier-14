// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Content.Server._NF.Cargo.Components;
using Content.Server.Botany.Components;
using Content.Server.Instruments;
using Content.Server.Nutrition.Components;
using Content.Shared._Lua.Market.Prototypes;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Construction.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.Materials;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Market.Systems;

public abstract class BaseMarketDynamicSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] protected readonly IPrototypeManager ProtoMan = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private string? _loadedDomainId;
    private double _cachedOnlineScale = 1.0;
    private TimeSpan _lastOnlineScaleUpdate = TimeSpan.Zero;
    private static readonly TimeSpan OnlineScaleCacheInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _lastCleanupTime = TimeSpan.Zero;
    private static readonly TimeSpan StateCleanupInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StateExpireTtl = TimeSpan.FromMinutes(30);
    private double _defaultDynamicDecayPerStack = 0.003;
    private double _defaultDynamicRestorePerMinute = 0.01;
    private double _defaultDynamicMinAfterTaxBaseFraction = 0.25;
    private double _defaultBulkDecayPerStack = 0.0007;
    private int _onlineMinBaseline = 0;
    private int _onlineMaxBaseline = 110;
    private double _onlineDecayScaleMin = 0.9;
    private double _onlineDecayScaleMax = 1.1;

    protected sealed class DynamicPriceState
    {
        public double NetSoldUnits = 0.0;
        public TimeSpan LastUpdateTime;
    }

    private int GetActiveAlivePlayerCount()
    {
        var sessions = _playerManager.Sessions;
        var count = 0;
        foreach (var s in sessions)
        {
            if (s.Status != SessionStatus.InGame) continue;
            if (s.AttachedEntity is not { } uid) continue;
            if (!EntityManager.TryGetComponent(uid, out MobStateComponent? mob)) continue;
            if (mob.CurrentState == MobState.Alive) count++;
        }
        return count;
    }

    private double GetOnlineDumpingScale()
    {
        var now = _timing.CurTime;
        if (now - _lastOnlineScaleUpdate < OnlineScaleCacheInterval) return _cachedOnlineScale;
        var online = (double)GetActiveAlivePlayerCount();
        double scale;
        if (online <= _onlineMinBaseline) scale = _onlineDecayScaleMin;
        else if (online >= _onlineMaxBaseline) scale = _onlineDecayScaleMax;
        else
        {
            var t = (online - _onlineMinBaseline) / Math.Max(1.0, (_onlineMaxBaseline - _onlineMinBaseline));
            scale = _onlineDecayScaleMin + t * (_onlineDecayScaleMax - _onlineDecayScaleMin);
        }
        _cachedOnlineScale = scale;
        _lastOnlineScaleUpdate = now;
        if (now - _lastCleanupTime > StateCleanupInterval || _dynamicPricing.Count > 2048)
        {
            CleanupExpiredStates(now);
            _lastCleanupTime = now;
        }
        return scale;
    }

    private void CleanupExpiredStates(TimeSpan now)
    {
        var toRemove = new List<string>();
        foreach (var (pid, state) in _dynamicPricing)
        { if (state.NetSoldUnits <= 0.0 && now - state.LastUpdateTime > StateExpireTtl) toRemove.Add(pid); }
        foreach (var pid in toRemove) _dynamicPricing.Remove(pid);
    }

    private enum MarketCategory
    {
        Chemistry,
        Botany,
        FoodDrink,
        MaterialsOres,
        ManufacturedTools,
        SalvageMisc,
        WeaponsSecurity,
        Instrument,
        Unknown
    }

    protected sealed class CategoryParams
    {
        public double DecayPerStack = 0.003;
        public double BulkDecayPerStack = 0.0007;
        public double RestorePerMinute = 0.01;
        public double MinAfterTaxBaseFraction = 0.25;
    }

    private static readonly Dictionary<MarketCategory, CategoryParams> CategoryConfig = new()
    {
        [MarketCategory.Chemistry] = new CategoryParams { DecayPerStack = 0.0100, BulkDecayPerStack = 0.0050, RestorePerMinute = 0.006, MinAfterTaxBaseFraction = 0.20 },
        [MarketCategory.Botany] = new CategoryParams { DecayPerStack = 0.0075, BulkDecayPerStack = 0.0038, RestorePerMinute = 0.01, MinAfterTaxBaseFraction = 0.25 },
        [MarketCategory.FoodDrink] = new CategoryParams { DecayPerStack = 0.0013, BulkDecayPerStack = 0.0005, RestorePerMinute = 0.015, MinAfterTaxBaseFraction = 0.35 },
        [MarketCategory.MaterialsOres] = new CategoryParams { DecayPerStack = 0.0008, BulkDecayPerStack = 0.0003, RestorePerMinute = 0.012, MinAfterTaxBaseFraction = 0.40 },
        [MarketCategory.ManufacturedTools] = new CategoryParams { DecayPerStack = 0.0018, BulkDecayPerStack = 0.0005, RestorePerMinute = 0.01, MinAfterTaxBaseFraction = 0.35 },
        [MarketCategory.SalvageMisc] = new CategoryParams { DecayPerStack = 0.0018, BulkDecayPerStack = 0.0005, RestorePerMinute = 0.01, MinAfterTaxBaseFraction = 0.30 },
        [MarketCategory.WeaponsSecurity] = new CategoryParams { DecayPerStack = 0.0038, BulkDecayPerStack = 0.0013, RestorePerMinute = 0.008, MinAfterTaxBaseFraction = 0.25 },
        [MarketCategory.Instrument] = new CategoryParams { DecayPerStack = 0.0, BulkDecayPerStack = 0.0, RestorePerMinute = 0.0, MinAfterTaxBaseFraction = 1.0 },
        [MarketCategory.Unknown] = new CategoryParams()
    };

    private readonly Dictionary<string, DynamicPriceState> _dynamicPricing = new();
    private float _domainBaseMultiplier = 1.0f;

    private DynamicPriceState GetState(string prototypeId)
    {
        if (!_dynamicPricing.TryGetValue(prototypeId, out var state))
        {
            state = new DynamicPriceState { NetSoldUnits = 0.0, LastUpdateTime = _timing.CurTime };
            _dynamicPricing[prototypeId] = state;
        }
        return state;
    }

    public void RestoreNow(string prototypeId)
    {
        var state = GetState(prototypeId);
        var now = _timing.CurTime;
        var elapsed = now - state.LastUpdateTime;
        if (elapsed <= TimeSpan.Zero) return;
        var minutes = elapsed.TotalMinutes;
        if (minutes <= 0)
        { state.LastUpdateTime = now; return; }
        var p = GetParamsForPrototype(prototypeId);
        var a = p.DecayPerStack;
        var restoreMultPerMinute = p.RestorePerMinute;
        if (a > 0.0)
        {
            var restoreUnitsPerMinute = restoreMultPerMinute / a;
            state.NetSoldUnits = Math.Max(0.0, state.NetSoldUnits - minutes * restoreUnitsPerMinute);
        }
        else
        { state.NetSoldUnits = 0.0; }
        state.LastUpdateTime = now;
    }

    public double GetCurrentDynamicMultiplier(string prototypeId)
    {
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var p = GetParamsForPrototype(prototypeId);
        var a = p.DecayPerStack;
        if (a <= 0.0) return 1.0 * _domainBaseMultiplier;
        var baseMult = Math.Max(0.0, 1.0 - a * state.NetSoldUnits);
        return baseMult * _domainBaseMultiplier;
    }

    public void RegisterSaleForEntity(EntityUid sold)
    {
        if (EntityManager.HasComponent<IgnoreMarketModifierComponent>(sold)) return;
        if (EntityManager.HasComponent<InstrumentComponent>(sold)) return;
        if (!EntityManager.TryGetComponent<MetaDataComponent>(sold, out var meta) || meta.EntityPrototype == null) return;
        string? prototypeId = meta.EntityPrototype.ID;
        var count = 1;
        if (EntityManager.TryGetComponent<StackComponent>(sold, out var stack))
        {
            var singularId = ProtoMan.Index<StackPrototype>(stack.StackTypeId).Spawn.Id;
            prototypeId = singularId;
        }
        if (prototypeId == null) return;
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        state.NetSoldUnits += count;
    }

    public void ApplyBulkSaleEffect(string prototypeId, int batchCount)
    {
        if (batchCount <= 1) return;
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var p = GetParamsForPrototype(prototypeId);
        var a = p.DecayPerStack;
        var scale = GetOnlineDumpingScale();
        if (a <= 0.0) return;
        var bulk = p.BulkDecayPerStack * scale;
        var extraUnits = (bulk / a) * Math.Max(0, batchCount - 1);
        state.NetSoldUnits = Math.Max(0.0, state.NetSoldUnits + extraUnits);
    }

    public double GetEffectiveMultiplierForBatch(string prototypeId, int batchCount)
    {
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var p = GetParamsForPrototype(prototypeId);
        var a = p.DecayPerStack;
        if (a <= 0.0) return 1.0 * _domainBaseMultiplier;
        var scale = GetOnlineDumpingScale();
        var bulk = p.BulkDecayPerStack * scale;
        var baseMult = Math.Max(0.0, 1.0 - a * state.NetSoldUnits);
        var extra = bulk * Math.Max(0, batchCount - 1);
        return Math.Max(0.0, baseMult - extra) * _domainBaseMultiplier;
    }

    public double GetProjectedMultiplierAfterSale(string prototypeId, int batchCount)
    {
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var p = GetParamsForPrototype(prototypeId);
        var a = p.DecayPerStack;
        if (a <= 0.0) return 1.0 * _domainBaseMultiplier;
        var scale = GetOnlineDumpingScale();
        var bulk = p.BulkDecayPerStack * scale * Math.Max(0, batchCount - 1);
        var baseAfter = Math.Max(0.0, 1.0 - a * Math.Max(0.0, state.NetSoldUnits + Math.Max(0, batchCount)));
        var projected = Math.Max(0.0, baseAfter - bulk);
        return projected * _domainBaseMultiplier;
    }

    public bool TryGetDynamicPrototypeId(EntityUid uid, out string prototypeId)
    {
        prototypeId = string.Empty;
        if (!EntityManager.TryGetComponent<MetaDataComponent>(uid, out var meta) || meta.EntityPrototype == null) return false;
        prototypeId = meta.EntityPrototype.ID;
        if (EntityManager.TryGetComponent<StackComponent>(uid, out var stack))
        {
            var singularId = ProtoMan.Index<StackPrototype>(stack.StackTypeId).Spawn.Id;
            prototypeId = singularId;
        }
        return true;
    }

    public void RegisterPurchaseForPrototype(string prototypeId, int units)
    {
        if (units <= 0) return;
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        state.NetSoldUnits = Math.Max(0.0, state.NetSoldUnits - units);
    }

    public double GetDynamicMinAfterTaxBaseFraction() => _defaultDynamicMinAfterTaxBaseFraction;
    public double GetDynamicMinAfterTaxBaseFraction(string prototypeId) => GetParamsForPrototype(prototypeId).MinAfterTaxBaseFraction;
    public void LoadDomainConfig(string prototypeId)
    {
        if (_loadedDomainId == prototypeId) return;
        if (!ProtoMan.TryIndex<MarketDomainConfigPrototype>(prototypeId, out var proto)) return;
        _loadedDomainId = prototypeId;
        _domainBaseMultiplier = proto.BaseMultiplier;
        _onlineMinBaseline = proto.OnlineMin;
        _onlineMaxBaseline = proto.OnlineMax;
        _onlineDecayScaleMin = proto.OnlineScaleMin;
        _onlineDecayScaleMax = proto.OnlineScaleMax;
        _defaultDynamicDecayPerStack = proto.DefaultDecayPerStack;
        _defaultBulkDecayPerStack = proto.DefaultBulkDecayPerStack;
        _defaultDynamicRestorePerMinute = proto.DefaultRestorePerMinute;
        _defaultDynamicMinAfterTaxBaseFraction = proto.DefaultMinAfterTaxBaseFraction;
        CategoryConfig[MarketCategory.Unknown] = new CategoryParams
        {
            DecayPerStack = _defaultDynamicDecayPerStack,
            BulkDecayPerStack = _defaultBulkDecayPerStack,
            RestorePerMinute = _defaultDynamicRestorePerMinute,
            MinAfterTaxBaseFraction = _defaultDynamicMinAfterTaxBaseFraction
        };
        foreach (var kv in proto.Categories)
        {
            var key = kv.Key;
            var cfg = kv.Value;
            if (!Enum.TryParse<MarketCategory>(key, out var cat)) continue;
            CategoryConfig[cat] = new CategoryParams
            {
                DecayPerStack = cfg.DecayPerStack,
                BulkDecayPerStack = cfg.BulkDecayPerStack,
                RestorePerMinute = cfg.RestorePerMinute,
                MinAfterTaxBaseFraction = cfg.MinAfterTaxBaseFraction
            };
        }
    }

    private CategoryParams GetParamsForPrototype(string prototypeId)
    {
        var category = GetCategoryForPrototype(prototypeId);
        if (CategoryConfig.TryGetValue(category, out var param)) return param;
        return new CategoryParams
        {
            DecayPerStack = _defaultDynamicDecayPerStack,
            BulkDecayPerStack = _defaultBulkDecayPerStack,
            RestorePerMinute = _defaultDynamicRestorePerMinute,
            MinAfterTaxBaseFraction = _defaultDynamicMinAfterTaxBaseFraction
        };
    }

    private MarketCategory GetCategoryForPrototype(string prototypeId)
    {
        if (!ProtoMan.TryIndex<EntityPrototype>(prototypeId, out var prototype))
            return MarketCategory.Unknown;

        if (prototype.TryGetComponent<InstrumentComponent>(out _, EntityManager.ComponentFactory)) return MarketCategory.Instrument;
        if (prototype.TryGetComponent<GunComponent>(out _, EntityManager.ComponentFactory) || prototype.TryGetComponent<MeleeWeaponComponent>(out _, EntityManager.ComponentFactory) || prototype.TryGetComponent<ExplosiveComponent>(out _, EntityManager.ComponentFactory)) return MarketCategory.WeaponsSecurity;
        if (prototype.TryGetComponent<FoodComponent>(out _, EntityManager.ComponentFactory) || prototype.TryGetComponent<DrinkComponent>(out _, EntityManager.ComponentFactory)) return MarketCategory.FoodDrink;
        if (prototype.TryGetComponent<ProduceComponent>(out _, EntityManager.ComponentFactory)) return MarketCategory.Botany;
        if (prototype.TryGetComponent<MaterialComponent>(out _, EntityManager.ComponentFactory)) return MarketCategory.MaterialsOres;
        if (prototype.TryGetComponent<ToolComponent>(out _, EntityManager.ComponentFactory) || prototype.TryGetComponent<MachinePartComponent>(out _, EntityManager.ComponentFactory)) return MarketCategory.ManufacturedTools;
        if (prototype.TryGetComponent<SolutionContainerManagerComponent>(out _, EntityManager.ComponentFactory)) return MarketCategory.Chemistry;
        return MarketCategory.SalvageMisc;
    }
}


