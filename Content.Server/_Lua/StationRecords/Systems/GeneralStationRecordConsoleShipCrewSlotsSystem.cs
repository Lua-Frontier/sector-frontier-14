// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.StationRecords.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._Lua.StationRecords;

namespace Content.Server._Lua.StationRecords.Systems;

public sealed class GeneralStationRecordConsoleShipCrewSlotsSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(EntityUid uid, GeneralStationRecordConsoleComponent comp, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, ShipCrewManagement.CaptainIdSlotId, comp.CaptainIdSlot);
        _itemSlots.AddItemSlot(uid, ShipCrewManagement.TargetIdSlotId, comp.TargetIdSlot);
    }

    private void OnRemove(EntityUid uid, GeneralStationRecordConsoleComponent comp, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, comp.CaptainIdSlot);
        _itemSlots.RemoveItemSlot(uid, comp.TargetIdSlot);
    }
}

