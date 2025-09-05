// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// Special Permission:
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project
// the right to use this code under a separate custom license agreement.

using Content.Server.Popups;
using Content.Server.PowerCell;
using Content.Shared._Lua.HardsuitSpeedBuff;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Timing;

namespace Content.Server._Lua.HardsuitSpeedBuff;

public sealed class HardsuitSpeedBuffSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    private readonly Dictionary<EntityUid, SpeedBuffData> _activeBuffs = new();

    private struct SpeedBuffData
    {
        public EntityUid Hardsuit;
        public EntityUid Wearer;
        public TimeSpan LastPowerCheck;
        public float PowerConsumptionRate;
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<HardsuitSpeedBuffComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<HardsuitSpeedBuffComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HardsuitSpeedBuffComponent, ActivateSpeedBuffActionEvent>(OnSpeedBuffActivate);
        SubscribeLocalEvent<HardsuitSpeedBuffComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<HardsuitSpeedBuffComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HardsuitSpeedBuffComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnPowerCellChanged(EntityUid uid, HardsuitSpeedBuffComponent comp, PowerCellChangedEvent args)
    {
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        ProcessActiveBuffs(frameTime);
    }

    private void ProcessActiveBuffs(float frameTime)
    {
        var currentTime = _timing.CurTime;
        var toRemove = new List<EntityUid>();
        foreach (var (hardsuit, data) in _activeBuffs)
        {
            if (!EntityManager.EntityExists(hardsuit) || !EntityManager.EntityExists(data.Wearer))
            {
                toRemove.Add(hardsuit);
                continue;
            }
            if (!TryComp<HardsuitSpeedBuffComponent>(hardsuit, out var comp))
            {
                toRemove.Add(hardsuit);
                continue;
            }
            if (currentTime - data.LastPowerCheck >= TimeSpan.FromMilliseconds(500))
            {
                if (!_cell.HasCharge(hardsuit, comp.PowerConsumption))
                {
                    DeactivateSpeedBuff(hardsuit, data.Wearer, Loc.GetString("hardsuit-speedbuff-power-low"));
                    toRemove.Add(hardsuit);
                    continue;
                }
                if (!_cell.TryUseCharge(hardsuit, comp.PowerConsumption))
                {
                    DeactivateSpeedBuff(hardsuit, data.Wearer, Loc.GetString("hardsuit-speedbuff-power-low"));
                    toRemove.Add(hardsuit);
                    continue;
                }
                _activeBuffs[hardsuit] = data with { LastPowerCheck = currentTime };
            }
        }
        foreach (var hardsuit in toRemove)
        {
            _activeBuffs.Remove(hardsuit);
        }
    }

    private void OnEquipped(EntityUid uid, HardsuitSpeedBuffComponent comp, GotEquippedEvent args)
    {
        if (comp.Activated && _cell.HasCharge(uid, comp.MinPowerRequired))
        {
            StartSpeedBuff(uid, comp, args.Equipee);
        }
    }

    private void OnUnequipped(EntityUid uid, HardsuitSpeedBuffComponent comp, GotUnequippedEvent args)
    {
        if (_activeBuffs.ContainsKey(uid))
        {
            DeactivateSpeedBuff(uid, args.Equipee, null);
            _activeBuffs.Remove(uid);
        }
    }

    private void OnGetActions(EntityUid uid, HardsuitSpeedBuffComponent comp, GetItemActionsEvent args)
    {
        args.AddAction(ref comp.ActionEntity, comp.Action);
    }

    private void OnSpeedBuffActivate(Entity<HardsuitSpeedBuffComponent> ent, ref ActivateSpeedBuffActionEvent args)
    {
        if (!TryComp<PowerCellDrawComponent>(ent.Owner, out var powerCell))
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-speedbuff-no-powercell"), ent.Owner, args.Performer);
            return;
        }
        if (!TryComp<ClothingSpeedModifierComponent>(ent.Owner, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuit-speedbuff-no-modifier"), ent.Owner, args.Performer);
            return;
        }
        if (ent.Comp.Activated)
        {
            DeactivateSpeedBuff(ent.Owner, args.Performer, Loc.GetString("hardsuit-speedbuff-deactivated"));
        }
        else
        {
            if (_cell.HasCharge(ent.Owner, ent.Comp.MinPowerRequired))
            {
                StartSpeedBuff(ent.Owner, ent.Comp, args.Performer);
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("hardsuit-speedbuff-insufficient-power"), ent.Owner, args.Performer);
            }
        }
    }

    private void StartSpeedBuff(EntityUid hardsuit, HardsuitSpeedBuffComponent comp, EntityUid wearer)
    {
        comp.Activated = true;
        if (TryComp<PowerCellDrawComponent>(hardsuit, out var powerCell))
        {
            powerCell.Enabled = true;
        }
        _activeBuffs[hardsuit] = new SpeedBuffData
        {
            Hardsuit = hardsuit,
            Wearer = wearer,
            LastPowerCheck = _timing.CurTime,
            PowerConsumptionRate = comp.PowerConsumption
        };
        _movement.RefreshMovementSpeedModifiers(wearer);
        _popupSystem.PopupEntity(Loc.GetString("hardsuit-speedbuff-activated"), hardsuit, wearer);
        Spawn("EffectSparks", Transform(wearer).Coordinates);
    }

    private void DeactivateSpeedBuff(EntityUid hardsuit, EntityUid wearer, string? message)
    {
        if (TryComp<HardsuitSpeedBuffComponent>(hardsuit, out var comp))
        {
            comp.Activated = false;
        }
        if (TryComp<PowerCellDrawComponent>(hardsuit, out var powerCell))
        {
            powerCell.Enabled = false;
        }
        _movement.RefreshMovementSpeedModifiers(wearer);
        if (!string.IsNullOrEmpty(message))
        {
            _popupSystem.PopupEntity(message, hardsuit, wearer);
        }
    }

    private void OnRefreshMovementSpeedModifiers(EntityUid uid, HardsuitSpeedBuffComponent comp, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (comp.Activated && _activeBuffs.ContainsKey(uid))
        {
            var data = _activeBuffs[uid];
            var walkMod = comp.WalkModifier;
            var sprintMod = comp.SprintModifier;
            args.Args.ModifySpeed(walkMod, sprintMod);
        }
    }
}

