// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Lua.Shared.Shipyard.Components;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server._NF.Shipyard.Systems;
public sealed partial class ShipyardSystem
{
    public bool TryPurchaseShuttleToDock(EntityUid consoleUid, EntityUid stationUid, ResPath shuttlePath, [NotNullWhen(true)] out EntityUid? shuttleEntityUid)
    {
        shuttleEntityUid = null;
        if (!TryComp<ShipyardConsoleComponent>(consoleUid, out _)) return false;
        if (TryComp<ShipyardLuaConsoleComponent>(consoleUid, out var lua) && lua.SelectedDockPort.HasValue)
        {
            var selectedDockEntity = GetEntity(lua.SelectedDockPort.Value);
            if (TryComp<DockingComponent>(selectedDockEntity, out var dockComp))
            {
                if (dockComp.Docked) return TryPurchaseShuttle(stationUid, shuttlePath, out shuttleEntityUid);
                var dockXform = Transform(selectedDockEntity);
                var targetGridUid = dockXform.GridUid;
                if (targetGridUid != null)
                {
                    if (TryAddShuttle(shuttlePath, out var shuttleGrid) && _gridAccess.TryGetShuttleGrid(shuttleGrid.Value, out var shuttleComponent))
                    {
                        var config = _docking.GetDockingConfigForGridDock( shuttleGrid.Value, targetGridUid.Value, selectedDockEntity, priorityTag: null, dockType: dockComp.DockType);
                        if (config != null)
                        {
                            _shuttle.FTLDock((shuttleGrid.Value, Transform(shuttleGrid.Value)), config);
                            shuttleEntityUid = shuttleGrid.Value;
                            return true;
                        }
                        else { QueueDel(shuttleGrid.Value); }
                    }
                }
            }
        }
        return TryPurchaseShuttle(stationUid, shuttlePath, out shuttleEntityUid);
    }
}

