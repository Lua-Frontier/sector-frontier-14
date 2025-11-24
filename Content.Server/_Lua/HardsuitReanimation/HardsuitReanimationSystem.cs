// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._Lua.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lua.HardsuitReanimation;

public sealed class HardsuitReanimationSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainers = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private readonly Dictionary<EntityUid, ReanimationData> _activeReanimations = new();

    private struct ReanimationData
    {
        public EntityUid Hardsuit;
        public EntityUid Wearer;
        public TimeSpan StartTime;
        public int CurrentStep;
        public bool IsTeleported;
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<HardsuitReanimationComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HardsuitReanimationComponent, GotUnequippedEvent>(OnUnequipped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        ProcessActiveReanimations(frameTime);
    }

    private void ProcessActiveReanimations(float frameTime)
    {
        var currentTime = _timing.CurTime;
        var toRemove = new List<EntityUid>();
        foreach (var (hardsuit, data) in _activeReanimations)
        {
            if (!EntityManager.EntityExists(hardsuit) || !EntityManager.EntityExists(data.Wearer))
            {
                toRemove.Add(hardsuit);
                continue;
            }
            if (!TryComp<HardsuitReanimationComponent>(hardsuit, out var comp))
            {
                toRemove.Add(hardsuit);
                continue;
            }
            var elapsed = currentTime - data.StartTime;
            var targetStep = (int)(elapsed.TotalSeconds / 4) + 1;
            if (targetStep > data.CurrentStep && targetStep <= 9)
            {
                ProcessReanimationStep(hardsuit, data.Wearer, targetStep, comp);
                _activeReanimations[hardsuit] = data with { CurrentStep = targetStep };
            }
            if (elapsed.TotalSeconds >= 36)
            {
                CompleteReanimation(hardsuit, data.Wearer, comp);
                toRemove.Add(hardsuit);
            }
        }
        foreach (var hardsuit in toRemove)
        {
            _activeReanimations.Remove(hardsuit);
        }
    }

    private void ProcessReanimationStep(EntityUid hardsuit, EntityUid wearer, int step, HardsuitReanimationComponent comp)
    {
        switch (step)
        {
            case 1: var deathMsg = Loc.GetString("hardsuit-reanimation-death-detected"); _popup.PopupEntity(deathMsg, wearer, wearer); _chat.TrySendInGameICMessage(hardsuit, deathMsg, InGameICChatType.Speak, true); break; case 2: var returnMsg = Loc.GetString("hardsuit-reanimation-return-home"); _popup.PopupEntity(returnMsg, wearer, wearer); _chat.TrySendInGameICMessage(hardsuit, returnMsg, InGameICChatType.Speak, true); break; case 3: TeleportToOrigin(wearer, comp); break; case 4: var procedureMsg = Loc.GetString("hardsuit-reanimation-procedure-start"); _popup.PopupEntity(procedureMsg, wearer, wearer); _chat.TrySendInGameICMessage(hardsuit, procedureMsg, InGameICChatType.Speak, true); break; case 5: break; case 6: PerformDefibrillation(wearer); break; case 7: var injectMsg = Loc.GetString("hardsuit-reanimation-inject-reagents"); _popup.PopupEntity(injectMsg, wearer, wearer); _chat.TrySendInGameICMessage(hardsuit, injectMsg, InGameICChatType.Speak, true); break; case 8: InjectReagents(wearer, comp); break; case 9: CheckReanimationResult(hardsuit, wearer, comp); break;
        }
    }

    private void TeleportToOrigin(EntityUid wearer, HardsuitReanimationComponent comp)
    {
        var transform = Transform(wearer);
        var defaultMapId = _gameTicker.DefaultMap;
        var originCoords = new MapCoordinates(0, 0, defaultMapId);
        Spawn(comp.EmpPulseEffect, transform.Coordinates);
        transform.Coordinates = EntityCoordinates.FromMap(_mapManager, originCoords);
        Spawn(comp.EmpPulseEffect, originCoords);
    }

    private void PerformDefibrillation(EntityUid wearer)
    {
        if (!TryComp<MobStateComponent>(wearer, out var mobState) ||
            !TryComp<MobThresholdsComponent>(wearer, out var thresholds))
        {return;}
        if (!_mobState.IsDead(wearer, mobState))
        {return;}
        var healDamage = new DamageSpecifier();
        healDamage.DamageDict.Add("Asphyxiation", -40);
        if (TryComp<DamageableComponent>(wearer, out var damageable))
        {
            _damageable.TryChangeDamage(wearer, healDamage, true);
        }
        if (_mobThreshold.TryGetThresholdForState(wearer, MobState.Dead, out var threshold) &&
            TryComp<DamageableComponent>(wearer, out var damageableComponent) &&
            damageableComponent.TotalDamage < threshold)
        {
            _mobState.ChangeMobState(wearer, MobState.Critical, mobState, wearer);
        }
        _audio.PlayPvs("/Audio/Items/Defib/defib_zap.ogg", wearer);
        Spawn("EffectSparks", Transform(wearer).Coordinates);
    }

    private void InjectReagents(EntityUid wearer, HardsuitReanimationComponent comp)
    {
        if (_solutionContainers.TryGetInjectableSolution(wearer, out var injectableSolution, out _))
        {
            _solutionContainers.TryAddReagent(injectableSolution.Value, "Ichor", FixedPoint2.New(comp.IchorAmount));
            _solutionContainers.TryAddReagent(injectableSolution.Value, "Omnizine", FixedPoint2.New(comp.OmnizineAmount));
        }
    }

    private void CheckReanimationResult(EntityUid hardsuit, EntityUid wearer, HardsuitReanimationComponent comp)
    {
        if (!TryComp<MobStateComponent>(wearer, out var mobState))
            return;
        if (mobState.CurrentState == MobState.Dead)
        {
            var failedMsg = Loc.GetString("hardsuit-reanimation-failed");
            _popup.PopupEntity(failedMsg, wearer, wearer);
            _chat.TrySendInGameICMessage(hardsuit, failedMsg, InGameICChatType.Speak, true);
            SendMedicalAlert(wearer);
        }
        else
        {
            comp.LastReanimationTime = _timing.CurTime;
            comp.IsReanimating = false;
            Dirty(hardsuit, comp);
        }
    }

    private void SendMedicalAlert(EntityUid wearer)
    {
        var transform = Transform(wearer);
        var pos = transform.MapPosition;
        var x = (int)pos.X;
        var y = (int)pos.Y;
        var posText = $"({x}, {y})";
        var station = _station.GetOwningStation(wearer);
        var stationText = station is null ? null : $"{Name(station.Value)} ";
        var mapText = "Unknown Map";
        if (transform.MapUid != null && TryComp<MetaDataComponent>(transform.MapUid.Value, out var mapMeta))
        {
            if (!string.IsNullOrWhiteSpace(mapMeta.EntityName))
                mapText = mapMeta.EntityName;
        }
        if (stationText == null)
            stationText = "";
        var speciesText = "";
        if (TryComp<HumanoidAppearanceComponent>(wearer, out var species))
            speciesText = $" ({species.Species})";
        var message = Loc.GetString("deathrattle-implant-dead-message",
            ("user", wearer),
            ("specie", speciesText),
            ("grid", stationText),
            ("map", mapText),
            ("position", posText));
        _radio.SendRadioMessage(wearer, message, "Medical", wearer);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        var uid = args.Target;
        var inventorySystem = EntitySystem.Get<InventorySystem>();
        if (!inventorySystem.TryGetSlotEntity(uid, "outerClothing", out var hardsuit) || hardsuit == null)
        {return;}
        if (!TryComp<HardsuitReanimationComponent>(hardsuit, out var comp))
        {return;}
        if (comp.IsReanimating)
        {return;}
        if (_timing.CurTime - comp.LastReanimationTime < comp.CooldownDuration)
        {return;}
        var hardsuitUid = hardsuit.Value;
        Timer.Spawn(comp.DeathDetectionDelay, () =>
        {
            if (!EntityManager.EntityExists(hardsuitUid) || !TryComp<HardsuitReanimationComponent>(hardsuitUid, out var comp))
            {return;}
            if (comp.IsReanimating)
            {return;}
            StartReanimationForWearer(hardsuitUid, uid, comp);
        });
    }

    private void StartReanimationForWearer(EntityUid hardsuit, EntityUid wearer, HardsuitReanimationComponent comp)
    {
        comp.IsReanimating = true;
        Dirty(hardsuit, comp);
        _activeReanimations[hardsuit] = new ReanimationData
        {
            Hardsuit = hardsuit,
            Wearer = wearer,
            StartTime = _timing.CurTime,
            CurrentStep = 0,
            IsTeleported = false
        };
    }

    private void CompleteReanimation(EntityUid hardsuit, EntityUid wearer, HardsuitReanimationComponent comp)
    {
        comp.IsReanimating = false;
        Dirty(hardsuit, comp);
    }

    private void OnEquipped(EntityUid uid, HardsuitReanimationComponent comp, GotEquippedEvent args)
    {
    }

    private void OnUnequipped(EntityUid uid, HardsuitReanimationComponent comp, GotUnequippedEvent args)
    {
        if (_activeReanimations.ContainsKey(uid))
        {
            _activeReanimations.Remove(uid);
            comp.IsReanimating = false;
            Dirty(uid, comp);
        }
    }
}
