// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

// Dynamic pricing subsystem for Frontier: time restoration, per-unit sale decay,
// and one-off bulk decay. Category detection is component-based. Instruments are excluded.
// RU: Подсистема динамики цен: восстановление по времени, поштучное и оптовое снижение.
using System;
using System.Collections.Generic;
using Content.Server._NF.Cargo.Components;
using Content.Shared.Stacks;
using Robust.Shared.Timing;
using Content.Shared.Chemistry.Components.SolutionManager; // Chemistry
using Content.Server.Nutrition.Components; // Food
using Content.Shared.Nutrition.Components; // Drink
using Content.Server.Botany.Components; // Botany
using Content.Shared.Materials; // Materials
using Content.Shared.Tools.Components; // Tools
using Content.Shared.Construction.Components; // Machine parts
using Robust.Shared.Prototypes; // EntityPrototype
using Content.Shared.Weapons.Ranged.Components; // Guns
using Content.Shared.Weapons.Melee; // Melee
using Content.Shared.Explosion.Components; // Explosives
using Content.Server.Instruments; // Instruments(?)
using Robust.Server.Player;
using Robust.Shared.Enums; // SessionStatus
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._NF.Market.Systems;

public sealed partial class MarketSystem
{
    // Timing dependency for dynamic pricing restoration.
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private sealed class DynamicPriceState
    {
        public double Multiplier = 1.0; // 1.0 == 100%
        public TimeSpan LastUpdateTime;
    }

    // Default fallbacks for category-less items
    private const double DefaultDynamicDecayPerStack = 0.01;
    private const double DefaultDynamicRestorePerMinute = 0.01;
    private const double DefaultDynamicMinAfterTaxBaseFraction = 0.25;
    private const double DefaultBulkDecayPerStack = 0.0007;

    // Online-dependent dumping tuning.
    private const int OnlineMinBaseline = 0;   // Min online
    private const int OnlineMaxBaseline = 110;  // Max online on this server
    private const double OnlineDecayScaleMin = 0.9;  // Softer scaling at low online
    private const double OnlineDecayScaleMax = 1.1;  // Softer scaling at high online

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
            if (!_entityManager.TryGetComponent(uid, out MobStateComponent? mob))
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

    private enum MarketCategory
    {
        Chemistry,
        Botany,
        FoodDrink,
        MaterialsOres,
        ManufacturedTools,
        SalvageMisc,
        WeaponsSecurity,
        Instrument, // special case: excluded from dynamic pricing. I hung it up just in case, because I don't know what kind of component it is, it was done on the advice of.
        Unknown
    }

    private sealed class CategoryParams
    {
        public double DecayPerStack;
        public double BulkDecayPerStack;
        public double RestorePerMinute;
        public double MinAfterTaxBaseFraction;
    }

    private static readonly Dictionary<MarketCategory, CategoryParams> CategoryConfig = new()
    {
        // Strongest debuff
        [MarketCategory.Chemistry] = new CategoryParams
        {
            DecayPerStack = 0.04,
            BulkDecayPerStack = 0.02,
            RestorePerMinute = 0.006,
            MinAfterTaxBaseFraction = 0.20
        },
        [MarketCategory.Botany] = new CategoryParams
        {
            DecayPerStack = 0.03,
            BulkDecayPerStack = 0.015,
            RestorePerMinute = 0.01,
            MinAfterTaxBaseFraction = 0.25
        },
        // Minimal debuff
        [MarketCategory.FoodDrink] = new CategoryParams
        {
            DecayPerStack = 0.005,
            BulkDecayPerStack = 0.002,
            RestorePerMinute = 0.015,
            MinAfterTaxBaseFraction = 0.35
        },
        // Weakest debuff
        [MarketCategory.MaterialsOres] = new CategoryParams
        {
            DecayPerStack = 0.003,
            BulkDecayPerStack = 0.001,
            RestorePerMinute = 0.012,
            MinAfterTaxBaseFraction = 0.40
        },
        // Minimal dumping
        [MarketCategory.ManufacturedTools] = new CategoryParams
        {
            DecayPerStack = 0.007,
            BulkDecayPerStack = 0.002,
            RestorePerMinute = 0.01,
            MinAfterTaxBaseFraction = 0.35
        },
        [MarketCategory.SalvageMisc] = new CategoryParams
        {
            DecayPerStack = 0.007,
            BulkDecayPerStack = 0.002,
            RestorePerMinute = 0.01,
            MinAfterTaxBaseFraction = 0.30
        },
        // Medium dumping
        [MarketCategory.WeaponsSecurity] = new CategoryParams
        {
            DecayPerStack = 0.015,
            BulkDecayPerStack = 0.005,
            RestorePerMinute = 0.008,
            MinAfterTaxBaseFraction = 0.25
        },
        // Musical instruments — excluded from dynamic pricing entirely
        [MarketCategory.Instrument] = new CategoryParams
        {
            DecayPerStack = 0.0,
            BulkDecayPerStack = 0.0,
            RestorePerMinute = 0.0,
            MinAfterTaxBaseFraction = 1.0
        },
        // Unknown fallback
        [MarketCategory.Unknown] = new CategoryParams
        {
            DecayPerStack = DefaultDynamicDecayPerStack,
            BulkDecayPerStack = DefaultBulkDecayPerStack,
            RestorePerMinute = DefaultDynamicRestorePerMinute,
            MinAfterTaxBaseFraction = DefaultDynamicMinAfterTaxBaseFraction
        }
    };

    private readonly Dictionary<string, DynamicPriceState> _dynamicPricing = new();

    private DynamicPriceState GetState(string prototypeId)
    {
        if (!_dynamicPricing.TryGetValue(prototypeId, out var state))
        {
            state = new DynamicPriceState { Multiplier = 1.0, LastUpdateTime = _timing.CurTime };
            _dynamicPricing[prototypeId] = state;
        }
        return state;
    }

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

    /// <summary>
    /// Returns current dynamic multiplier for a prototype after applying time-based restoration.
    /// </summary>
    public double GetCurrentDynamicMultiplier(string prototypeId)
    {
        RestoreNow(prototypeId);
        return GetState(prototypeId).Multiplier;
    }

    /// <summary>
    /// Register sale for an entity. Applies dynamic decay by unit count, excluding items that ignore market modifiers.
    /// </summary>
    public void RegisterSaleForEntity(EntityUid sold)
    {
        // Ignore items with IgnoreMarketModifier.
        if (_entityManager.HasComponent<IgnoreMarketModifierComponent>(sold))
            return;
        // Ignore musical instruments entirely.
        if (_entityManager.HasComponent<InstrumentComponent>(sold))
            return;

        if (!_entityManager.TryGetComponent<MetaDataComponent>(sold, out var meta) || meta.EntityPrototype == null)
            return;

        string? prototypeId = meta.EntityPrototype.ID;
        var count = 1; // Treat a whole stack as 1 unit for decay.

        if (_entityManager.TryGetComponent<StackComponent>(sold, out var stack))
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
        // LastUpdateTime remains current from RestoreNow.
    }

    /// <summary>
        /// Applies one-off extra bulk decay when multiple units of the same prototype are sold at once.
    /// </summary>
    /// <param name="prototypeId">Prototype id whose dynamic price is affected.</param>
    /// <param name="batchCount">How many units of this prototype were sold in the batch.</param>
    public void ApplyBulkSaleEffect(string prototypeId, int batchCount) //Lua
    {
        if (batchCount <= 1)
            return;

        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var bulk = GetParamsForPrototype(prototypeId).BulkDecayPerStack * GetOnlineDumpingScale();
        var extra = bulk * Math.Max(0, batchCount - 1);
        state.Multiplier = Math.Max(0.0, state.Multiplier - extra);
    }

    /// <summary>
        /// Returns an effective multiplier for a given batch size, without mutating state.
        /// Used for preview and current-sale calculation.
    /// </summary>
    public double GetEffectiveMultiplierForBatch(string prototypeId, int batchCount)
    {
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var bulk = GetParamsForPrototype(prototypeId).BulkDecayPerStack * GetOnlineDumpingScale();
        var extra = bulk * Math.Max(0, batchCount - 1);
        return Math.Max(0.0, state.Multiplier - extra);
    }

    /// <summary>
    /// Calculates the projected dynamic multiplier after completing a sale batch of the given size.
    /// Includes both per-unit decay and the one-off bulk effect to reflect the real post-sale state.
    /// This does NOT modify the stored state.
    /// </summary>
    /// <param name="prototypeId">Prototype id to evaluate.</param>
    /// <param name="batchCount">Units of this prototype in the sale batch (stacks expanded).</param>
    /// <returns>Projected multiplier (0..1) immediately after the sale completes.</returns>
    public double GetProjectedMultiplierAfterSale(string prototypeId, int batchCount)
    {
        RestoreNow(prototypeId);
        var state = GetState(prototypeId);
        var p = GetParamsForPrototype(prototypeId);
        var scale = GetOnlineDumpingScale();
        var perUnit = p.DecayPerStack * scale * Math.Max(0, batchCount);
        var bulk = p.BulkDecayPerStack * scale * Math.Max(0, batchCount - 1);
        var projected = state.Multiplier - perUnit - bulk;
        return Math.Max(0.0, projected);
    }

    /// <summary>
        /// Helper: find prototype id used for dynamic pricing for an entity.
    /// </summary>
    public bool TryGetDynamicPrototypeId(EntityUid uid, out string prototypeId)
    {
        prototypeId = string.Empty;
        if (!_entityManager.TryGetComponent<MetaDataComponent>(uid, out var meta) || meta.EntityPrototype == null)
            return false;

        prototypeId = meta.EntityPrototype.ID;
        if (_entityManager.TryGetComponent<StackComponent>(uid, out var stack))
        {
            var singularId = _prototypeManager.Index<StackPrototype>(stack.StackTypeId).Spawn.Id;
            prototypeId = singularId;
        }
        return true;
    }

    /// <summary>
        /// Default minimum fraction of base price after tax used by other systems.
    /// </summary>
    public double GetDynamicMinAfterTaxBaseFraction() => DefaultDynamicMinAfterTaxBaseFraction;

    /// <summary>
        /// Category-specific minimum fraction of base price after tax for a prototype.
    /// </summary>
    public double GetDynamicMinAfterTaxBaseFraction(string prototypeId)
    {
        return GetParamsForPrototype(prototypeId).MinAfterTaxBaseFraction;
    }

    private CategoryParams GetParamsForPrototype(string prototypeId)
    {
        var category = GetCategoryForPrototype(prototypeId);
        if (CategoryConfig.TryGetValue(category, out var param))
            return param;
        return CategoryConfig[MarketCategory.Unknown];
    }

    private MarketCategory GetCategoryForPrototype(string prototypeId)
    {
        if (!_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var prototype))
            return MarketCategory.Unknown;

        // Exclude musical instruments
        if (prototype.TryGetComponent<InstrumentComponent>(out _, _componentFactory))
            return MarketCategory.Instrument;

        // Weapons & Security
        if (prototype.TryGetComponent<GunComponent>(out _, _componentFactory)
            || prototype.TryGetComponent<MeleeWeaponComponent>(out _, _componentFactory)
            || prototype.TryGetComponent<ExplosiveComponent>(out _, _componentFactory))
            return MarketCategory.WeaponsSecurity;

        // Food & Drink — detect before Chemistry so they don't get classified as Chemistry
        if (prototype.TryGetComponent<FoodComponent>(out _, _componentFactory)
            || prototype.TryGetComponent<DrinkComponent>(out _, _componentFactory))
            return MarketCategory.FoodDrink;

        // Botany
        if (prototype.TryGetComponent<ProduceComponent>(out _, _componentFactory))
            return MarketCategory.Botany;

        // Materials & Ores
        if (prototype.TryGetComponent<MaterialComponent>(out _, _componentFactory))
            return MarketCategory.MaterialsOres;

        // Manufactured & Tools
        if (prototype.TryGetComponent<ToolComponent>(out _, _componentFactory)
            || prototype.TryGetComponent<MachinePartComponent>(out _, _componentFactory))
            return MarketCategory.ManufacturedTools;

        // Chemistry — any entities with solution containers
        if (prototype.TryGetComponent<SolutionContainerManagerComponent>(out _, _componentFactory))
            return MarketCategory.Chemistry;

        // Salvage & Misc — fallback
        return MarketCategory.SalvageMisc;
    }
}


