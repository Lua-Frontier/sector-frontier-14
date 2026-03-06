// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Shipyard.Systems;
using Content.Shared._Lua.Shipyard.BUI;
using Content.Shared._Mono.Ships.Components;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Components;

namespace Content.Server._NF.Shipyard.Systems;

public sealed partial class ShipyardSystem
{
    [Dependency] private readonly ShuttleParkingSystem _parking = default!;

    private bool HandleParkingPurchase(EntityUid consoleUid, ShipyardConsoleComponent component, EntityUid player, EntityUid targetId)
    {
        if (!TryComp<ShuttleDeedComponent>(targetId, out var deed) || deed.ShuttleUid is not { Valid: true } shuttleUid)
        {
            ConsolePopup(player, Loc.GetString("shipyard-console-no-deed"));
            PlayDenySound(player, consoleUid, component);
            return true;
        }
        if (component.SelectedDockPort is not { } netDock)
        {
            ConsolePopup(player, Loc.GetString("shipyard-console-parking-no-dock-selected"));
            PlayDenySound(player, consoleUid, component);
            return true;
        }
        var dockUid = GetEntity(netDock);
        var result = _parking.TryRecallShuttle(consoleUid, shuttleUid, dockUid);
        if (result.Error != ShuttleParkingSystem.ShuttleParkingError.Success)
        {
            ConsolePopup(player, Loc.GetString(GetParkingErrorLoc(result)));
            PlayDenySound(player, consoleUid, component);
            return true;
        }
        PlayConfirmSound(player, consoleUid, component);
        RefreshParkingState(consoleUid, GetFullName(deed), targetId);
        return true;
    }

    private bool HandleParkingSell(EntityUid consoleUid, ShipyardConsoleComponent component, EntityUid player, EntityUid targetId)
    {
        if (!TryComp<ShuttleDeedComponent>(targetId, out var deed) || deed.ShuttleUid is not { Valid: true } shuttleUid)
        {
            ConsolePopup(player, Loc.GetString("shipyard-console-no-deed"));
            PlayDenySound(player, consoleUid, component);
            return true;
        }
        var result = _parking.TryParkShuttle(consoleUid, shuttleUid);
        if (result.Error != ShuttleParkingSystem.ShuttleParkingError.Success)
        {
            var errorLoc = GetParkingErrorLoc(result);
            if (result.Error == ShuttleParkingSystem.ShuttleParkingError.OrganicsAboard) ConsolePopup(player, Loc.GetString(errorLoc, ("name", result.OrganicName ?? "Somebody")));
            else ConsolePopup(player, Loc.GetString(errorLoc));
            PlayDenySound(player, consoleUid, component);
            return true;
        }
        PlayConfirmSound(player, consoleUid, component);
        RefreshParkingState(consoleUid, GetFullName(deed), targetId);
        return true;
    }

    private static string GetParkingErrorLoc(ShuttleParkingSystem.ShuttleParkingResult result)
    {
        return result.Error switch
        {
            ShuttleParkingSystem.ShuttleParkingError.AlreadyParked => "shipyard-console-parking-already-parked",
            ShuttleParkingSystem.ShuttleParkingError.ShuttleNotParked => "shipyard-console-parking-not-parked",
            ShuttleParkingSystem.ShuttleParkingError.NotDocked => "shipyard-console-sale-not-docked",
            ShuttleParkingSystem.ShuttleParkingError.OrganicsAboard => "shipyard-console-sale-organic-aboard",
            ShuttleParkingSystem.ShuttleParkingError.CryoPodAboard => "shipyard-console-parking-cryo-pod-aboard",
            ShuttleParkingSystem.ShuttleParkingError.InvalidDock => "shipyard-console-parking-invalid-dock",
            ShuttleParkingSystem.ShuttleParkingError.NoDockingPath => "shipyard-console-parking-no-docking-path",
            ShuttleParkingSystem.ShuttleParkingError.InvalidConsole => "shipyard-console-invalid-station",
            _ => "shipyard-console-sale-invalid-ship",
        };
    }

    private bool TryGetAvailableParkingShuttles(EntityUid uid, EntityUid? targetId, out List<string> available, out List<string> unavailable)
    {
        available = new List<string>();
        unavailable = new List<string>();
        if (TryComp<ShipyardConsoleComponent>(uid, out var console) && console.ParkingConsole)
        {
            if (targetId is { Valid: true } insertedId && TryComp<ShuttleDeedComponent>(insertedId, out var deed) && deed.ShuttleUid is { Valid: true } shuttleUid && _parking.IsParked(shuttleUid) && TryComp<VesselComponent>(shuttleUid, out var vessel))
            { available.Add(vessel.VesselId.ToString()); }
            return true;
        }
        return false;
    }

    private void RefreshParkingState(EntityUid uid, string? shipDeed, EntityUid? targetId)
    {
        var parked = false;
        if (targetId is { Valid: true } insertedId && TryComp<ShuttleDeedComponent>(insertedId, out var deed) && deed.ShuttleUid is { Valid: true } shuttleUid)
        { parked = _parking.IsParked(shuttleUid); }
        BoundUserInterfaceState state = new ParkingConsoleInterfaceState(shipDeed, targetId.HasValue, parked);
        ExtendUiStateLua(uid, ref state);
        _ui.SetUiState(uid, ShipyardConsoleUiKey.Parking, state);
    }
}
