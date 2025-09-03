// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

// Dynamic pricing base system used to provide isolated instances per console group.
// This system tracks and updates per-prototype multipliers over time and on sales,
// and supports category-specific parameters loaded from prototypes.
using System;
using System.Collections.Generic;
using Content.Server._NF.Cargo.Components;
using Content.Server.Instruments;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Server.Nutrition.Components;
using Content.Shared.Nutrition.Components;
using Content.Server.Botany.Components;
using Content.Shared.Materials;
using Content.Shared.Tools.Components;
using Content.Shared.Construction.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing; // IGameTiming
using Content.Shared.Stacks;
using Robust.Server.Player;
using Robust.Shared.Enums; // SessionStatus
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._Lua.Market.Systems;

/// <summary>
/// Базовая серверная система динамического ценообразования.
/// Хранит множители по прототипам, восстанавливает их со временем,
/// применяет поштучное и оптовое снижение, поддерживает параметры домена/категорий из MarketDomainConfigPrototype.
/// </summary>
public abstract class BaseMarketDynamicSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] protected readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    // Состояние: множитель для прототипа и время последнего обновления.
    protected sealed class DynamicPriceState
    {
        public double Multiplier = 1.0;
        public TimeSpan LastUpdateTime;
    }

    private double DefaultDynamicDecayPerStack = 0.003;
    private double DefaultDynamicRestorePerMinute = 0.01;
    private double DefaultDynamicMinAfterTaxBaseFraction = 0.25;
    private double DefaultBulkDecayPerStack = 0.0007;

        // Online-dependent dumping tuning.
        private int OnlineMinBaseline = 0;   // Min online
        private int OnlineMaxBaseline = 110; // Max online
        private double OnlineDecayScaleMin = 0.9;  // Softer scaling at low online
        private double OnlineDecayScaleMax = 1.1;  // Softer scaling at high online

        private int GetActiveAlivePlayerCount()
        {
            var sessions = _playerManager.Sessions;
            var count = 0;
            foreach (var s in sessions)
            {
                if (s.Status != SessionStatus.InGame)
                    continue;
                if (s.AttachedEntity is not { } uid)
                    continue;
                if (!EntityManager.TryGetComponent(uid, out MobStateComponent? mob))
                    continue;
                if (mob.CurrentState == MobState.Alive)
                    count++;
            }
            return count;
        }

        private double GetOnlineDumpingScale()
        {
            var online = (double) GetActiveAlivePlayerCount();
            if (online <= OnlineMinBaseline)
                return OnlineDecayScaleMin;
            if (online >= OnlineMaxBaseline)
                return OnlineDecayScaleMax;
            var t = (online - OnlineMinBaseline) / Math.Max(1.0, (OnlineMaxBaseline - OnlineMinBaseline));
            return OnlineDecayScaleMin + t * (OnlineDecayScaleMax - OnlineDecayScaleMin);
        }

    // Категории товаров для подбора параметров динамики.
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

    // Параметры динамики на уровне категории.
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
            state = new DynamicPriceState { Multiplier = 1.0, LastUpdateTime = _timing.CurTime };
            _dynamicPricing[prototypeId] = state;
        }
        return state;
    }

    // Применяет восстановление множителя по прошествии времени.
    public void RestoreNow(string prototypeId)
    {
        var state = GetState(prototypeId);
        var now = _timing.CurTime;
        var elapsed = now - state.LastUpdateTime;
        if (elapsed <= TimeSpan.Zero)
            return;

        var minutes = elapsed.TotalMinutes;
        if (minutes <= 0)
        {
            state.LastUpdateTime = now;
            return;
        }

        var restore = GetParamsForPrototype(prototypeId).RestorePerMinute;
        state.Multiplier = Math.Min(1.0, state.Multiplier + minutes * restore);
        state.LastUpdateTime = now;
    }

    // Возвращает текущий множитель после восстановления.
    public double GetCurrentDynamicMultiplier(string prototypeId)
    {
        RestoreNow(prototypeId);
        return GetState(prototypeId).Multiplier * _domainBaseMultiplier;
    }

    // Регистрирует продажу и применяет поштучное снижение; исключения пропускаются.
    public void RegisterSaleForEntity(EntityUid sold)
    {
        if (EntityManager.HasComponent<IgnoreMarketModifierComponent>(sold))
            return;
        if (EntityManager.HasComponent<InstrumentComponent>(sold))
            return;

        if (!EntityManager.TryGetComponent<MetaDataComponent>(sold, out var meta) || meta.EntityPrototype == null)
            return;

        string? prototypeId = meta.EntityPrototype.ID;
        var count = 1; // stack = 1

        if (EntityManager.TryGetComponent<StackComponent>(sold, out var stack))
        {
            var singularId = _prototypeManager.Index<StackPrototype>(stack.StackTypeId).Spawn.Id;
            prototypeId = singularId;
        }

        if (prototypeId == null)
            return;

        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var decay = GetParamsForPrototype(prototypeId).DecayPerStack * GetOnlineDumpingScale();
        state.Multiplier = Math.Max(0.0, state.Multiplier - decay * count);
    }

    // Применяет разовое оптовое снижение при продаже пачкой.
    public void ApplyBulkSaleEffect(string prototypeId, int batchCount)
    {
        if (batchCount <= 1)
            return;

        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var bulk = GetParamsForPrototype(prototypeId).BulkDecayPerStack * GetOnlineDumpingScale();
        var extra = bulk * Math.Max(0, batchCount - 1);
        state.Multiplier = Math.Max(0.0, state.Multiplier - extra);
    }

    // Возвращает прогноз множителя для размера партии без изменения состояния.
    public double GetEffectiveMultiplierForBatch(string prototypeId, int batchCount)
    {
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var bulk = GetParamsForPrototype(prototypeId).BulkDecayPerStack * GetOnlineDumpingScale();
        var extra = bulk * Math.Max(0, batchCount - 1);
        return Math.Max(0.0, state.Multiplier - extra) * _domainBaseMultiplier;
    }

    // Считает множитель сразу после завершения продажи партии.
    public double GetProjectedMultiplierAfterSale(string prototypeId, int batchCount)
    {
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var p = GetParamsForPrototype(prototypeId);
        var scale = GetOnlineDumpingScale();
        var perUnit = p.DecayPerStack * scale * Math.Max(0, batchCount);
        var bulk = p.BulkDecayPerStack * scale * Math.Max(0, batchCount - 1);
        var projected = state.Multiplier - perUnit - bulk;
        return Math.Max(0.0, projected) * _domainBaseMultiplier;
    }

    // Определяет прототип для динамики (стаки приводятся к единичному).
    public bool TryGetDynamicPrototypeId(EntityUid uid, out string prototypeId)
    {
        prototypeId = string.Empty;
        if (!EntityManager.TryGetComponent<MetaDataComponent>(uid, out var meta) || meta.EntityPrototype == null)
            return false;

        prototypeId = meta.EntityPrototype.ID;
        if (EntityManager.TryGetComponent<StackComponent>(uid, out var stack))
        {
            var singularId = _prototypeManager.Index<StackPrototype>(stack.StackTypeId).Spawn.Id;
            prototypeId = singularId;
        }
        return true;
    }

    public double GetDynamicMinAfterTaxBaseFraction() => DefaultDynamicMinAfterTaxBaseFraction; // RU: Минимальная доля от базовой цены после налога (по умолчанию)
    public double GetDynamicMinAfterTaxBaseFraction(string prototypeId) => GetParamsForPrototype(prototypeId).MinAfterTaxBaseFraction;

    // Загружает конфигурацию домена по id прототипа; при отсутствии — оставляет значения по умолчанию.
    public void LoadDomainConfig(string prototypeId)
    {
        // Find domain config. If missing, keep defaults.
        if (!_prototypeManager.TryIndex<Content.Shared._Lua.Market.Prototypes.MarketDomainConfigPrototype>(prototypeId, out var proto))
            return;
        _domainBaseMultiplier = proto.BaseMultiplier;

        // Online scaling from prototype
        OnlineMinBaseline = proto.OnlineMin;
        OnlineMaxBaseline = proto.OnlineMax;
        OnlineDecayScaleMin = proto.OnlineScaleMin;
        OnlineDecayScaleMax = proto.OnlineScaleMax;

        // Default dynamic parameters from prototype
        DefaultDynamicDecayPerStack = proto.DefaultDecayPerStack;
        DefaultBulkDecayPerStack = proto.DefaultBulkDecayPerStack;
        DefaultDynamicRestorePerMinute = proto.DefaultRestorePerMinute;
        DefaultDynamicMinAfterTaxBaseFraction = proto.DefaultMinAfterTaxBaseFraction;

        // Ensure Unknown category reflects defaults
        CategoryConfig[MarketCategory.Unknown] = new CategoryParams
        {
            DecayPerStack = DefaultDynamicDecayPerStack,
            BulkDecayPerStack = DefaultBulkDecayPerStack,
            RestorePerMinute = DefaultDynamicRestorePerMinute,
            MinAfterTaxBaseFraction = DefaultDynamicMinAfterTaxBaseFraction
        };

        foreach (var kv in proto.Categories)
        {
            var key = kv.Key;
            var cfg = kv.Value;
            if (!Enum.TryParse<MarketCategory>(key, out var cat))
                continue;
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
        if (CategoryConfig.TryGetValue(category, out var param))
            return param;
        return new CategoryParams
        {
            DecayPerStack = DefaultDynamicDecayPerStack,
            BulkDecayPerStack = DefaultBulkDecayPerStack,
            RestorePerMinute = DefaultDynamicRestorePerMinute,
            MinAfterTaxBaseFraction = DefaultDynamicMinAfterTaxBaseFraction
        };
    }

    /// <summary>
    /// Attempts to classify a prototype into a pricing category based on attached components.
    /// </summary>
    private MarketCategory GetCategoryForPrototype(string prototypeId)
    {
        if (!_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var prototype))
            return MarketCategory.Unknown;

        if (prototype.TryGetComponent<InstrumentComponent>(out _, EntityManager.ComponentFactory))
            return MarketCategory.Instrument;
        if (prototype.TryGetComponent<Content.Shared.Weapons.Ranged.Components.GunComponent>(out _, EntityManager.ComponentFactory)
            || prototype.TryGetComponent<Content.Shared.Weapons.Melee.MeleeWeaponComponent>(out _, EntityManager.ComponentFactory)
            || prototype.TryGetComponent<Content.Shared.Explosion.Components.ExplosiveComponent>(out _, EntityManager.ComponentFactory))
            return MarketCategory.WeaponsSecurity;
        if (prototype.TryGetComponent<FoodComponent>(out _, EntityManager.ComponentFactory)
            || prototype.TryGetComponent<DrinkComponent>(out _, EntityManager.ComponentFactory))
            return MarketCategory.FoodDrink;
        if (prototype.TryGetComponent<ProduceComponent>(out _, EntityManager.ComponentFactory))
            return MarketCategory.Botany;
        if (prototype.TryGetComponent<MaterialComponent>(out _, EntityManager.ComponentFactory))
            return MarketCategory.MaterialsOres;
        if (prototype.TryGetComponent<ToolComponent>(out _, EntityManager.ComponentFactory)
            || prototype.TryGetComponent<MachinePartComponent>(out _, EntityManager.ComponentFactory))
            return MarketCategory.ManufacturedTools;
        if (prototype.TryGetComponent<SolutionContainerManagerComponent>(out _, EntityManager.ComponentFactory))
            return MarketCategory.Chemistry;
        return MarketCategory.SalvageMisc;
    }
}


