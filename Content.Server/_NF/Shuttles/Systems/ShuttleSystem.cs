// New Frontiers - This file is licensed under AGPLv3
// Copyright (c) 2024 New Frontiers Contributors
// See AGPLv3.txt for details.

using Content.Server._NF.Station.Components;
using Content.Server.Shuttles.Components;
using Content.Server._Lua.Shuttles.Systems; // Lua
using Content.Shared._NF.Shuttles.Events;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    [Dependency] private readonly RadarConsoleSystem _radarConsole = default!;
    [Dependency] private readonly ShuttleTabletSystem _tablet = default!; // Lua

    private const float SpaceFrictionStrength = 0.0075f;
    private const float DampenDampingStrength = 0.25f;
    private const float AnchorDampingStrength = 2.5f;
    private void NfInitialize()
    {
        SubscribeLocalEvent<ShuttleConsoleComponent, SetInertiaDampeningRequest>(OnSetInertiaDampening);
        SubscribeLocalEvent<ShuttleConsoleComponent, SetServiceFlagsRequest>(NfSetServiceFlags);
        SubscribeLocalEvent<ShuttleConsoleComponent, SetTargetCoordinatesRequest>(NfSetTargetCoordinates);
        SubscribeLocalEvent<ShuttleConsoleComponent, SetHideTargetRequest>(NfSetHideTarget);
    }

    private bool SetInertiaDampening(EntityUid uid, ShuttleComponent shuttleComponent, EntityUid shuttle, InertiaDampeningMode mode) // Lua
    {
        /* Lua start

        if (!transform.GridUid.HasValue)
        {
            return false;
        }

           Lua end */

        if (mode == InertiaDampeningMode.Query)
        {
            _console.RefreshShuttleConsoles(shuttle); // Lua
            return false;
        }

        if (!EntityManager.HasComponent<ShuttleDeedComponent>(shuttle) || // Lua
            EntityManager.HasComponent<StationDampeningComponent>(_station.GetOwningStation(shuttle))) // Lua
        {
            return false;
        }

        shuttleComponent.BodyModifier = mode switch
        {
            InertiaDampeningMode.Off => SpaceFrictionStrength,
            InertiaDampeningMode.Dampen => DampenDampingStrength,
            InertiaDampeningMode.Anchor => AnchorDampingStrength,
            _ => DampenDampingStrength, // other values: default to some sane behaviour (assume normal dampening)
        };

        if (shuttleComponent.DampingModifier != 0)
            shuttleComponent.DampingModifier = shuttleComponent.BodyModifier;
        _console.RefreshShuttleConsoles(shuttle); // Lua
        return true;
    }

    private void OnSetInertiaDampening(EntityUid uid, ShuttleConsoleComponent component, SetInertiaDampeningRequest args)
    {
        // Lua start
        if (!TryGetShuttle(uid, out var gridUid))
        {
            return;
        }

        if (!EntityManager.TryGetComponent(gridUid, out ShuttleComponent? shuttleComponent))
        {
            return;
        }
        // Lua end

        if (SetInertiaDampening(uid, shuttleComponent, gridUid, args.Mode) && args.Mode != InertiaDampeningMode.Query) // Lua
            component.DampeningMode = args.Mode;
    }

    public InertiaDampeningMode NfGetInertiaDampeningMode(EntityUid entity)
    {
        // Lua start
        if (!TryGetShuttle(entity, out var gridUid))
        {
            return InertiaDampeningMode.Dampen;
        }
        // Lua end

        // Not a shuttle, shouldn't be togglable
        if (!EntityManager.HasComponent<ShuttleDeedComponent>(gridUid) || // Lua
            EntityManager.HasComponent<StationDampeningComponent>(_station.GetOwningStation(gridUid))) // Lua
            return InertiaDampeningMode.Station;

        if (!EntityManager.TryGetComponent(gridUid, out ShuttleComponent? shuttle)) // Lua
            return InertiaDampeningMode.Dampen;

        if (shuttle.BodyModifier >= AnchorDampingStrength)
            return InertiaDampeningMode.Anchor;
        else if (shuttle.BodyModifier <= SpaceFrictionStrength)
            return InertiaDampeningMode.Off;
        else
            return InertiaDampeningMode.Dampen;
    }

    public void NfSetPowered(EntityUid uid, ShuttleConsoleComponent component, bool powered)
    {
        // Lua start
        if (!TryGetShuttle(uid, out var gridUid))
        {
            return;
        }

        if (!EntityManager.TryGetComponent(gridUid, out ShuttleComponent? shuttleComponent))
        {
            return;
        }
        // Lua end

        // Update dampening physics without adjusting requested mode.
        if (!powered)
        {
            SetInertiaDampening(uid, shuttleComponent, gridUid, InertiaDampeningMode.Anchor); // Lua
        }
        else
        {
            // Update our dampening mode if we need to, and if we aren't a station.
            var currentDampening = NfGetInertiaDampeningMode(uid);
            if (currentDampening != component.DampeningMode &&
                currentDampening != InertiaDampeningMode.Station &&
                component.DampeningMode != InertiaDampeningMode.Station)
            {
                SetInertiaDampening(uid, shuttleComponent, gridUid, component.DampeningMode); // Lua
            }
        }
    }

    /// <summary>
    /// Get the current service flags for this grid.
    /// </summary>
    public ServiceFlags NfGetServiceFlags(EntityUid uid)
    {
        // Lua start
        if (!TryGetShuttle(uid, out var gridUid))
        {
            return ServiceFlags.None;
        }
        // Lua end

        // Set the service flags on the IFFComponent.
        if (!EntityManager.TryGetComponent<IFFComponent>(gridUid, out var iffComponent))
            return ServiceFlags.None;

        return iffComponent.ServiceFlags;
    }

    /// <summary>
    /// Set the service flags for this grid.
    /// </summary>
    public void NfSetServiceFlags(EntityUid uid, ShuttleConsoleComponent component, SetServiceFlagsRequest args)
    {
        // Lua start
        if (!TryGetShuttle(uid, out var gridUid))
        {
            return;
        }
        // Lua end

        // Set the service flags on the IFFComponent.
        if (!EntityManager.TryGetComponent<IFFComponent>(gridUid, out var iffComponent))
            return;

        iffComponent.ServiceFlags = args.ServiceFlags;
        _console.RefreshShuttleConsoles(gridUid);
        Dirty(gridUid, iffComponent);
    }

    public void NfSetTargetCoordinates(EntityUid uid, ShuttleConsoleComponent component, SetTargetCoordinatesRequest args)
    {
        if (!TryComp<RadarConsoleComponent>(uid, out var radarConsole))
            return;

        // Lua start
        if (!TryGetShuttle(uid, out var gridUid))
        {
            return;
        }
        // Lua end

        _radarConsole.SetTarget((uid, radarConsole), args.TrackedEntity, args.TrackedPosition);
        _radarConsole.SetHideTarget((uid, radarConsole), false); // Force target visibility
        _console.RefreshShuttleConsoles(gridUid);
    }

    public void NfSetHideTarget(EntityUid uid, ShuttleConsoleComponent component, SetHideTargetRequest args)
    {
        if (!TryComp<RadarConsoleComponent>(uid, out var radarConsole))
            return;

        // Lua start
        if (!TryGetShuttle(uid, out var gridUid))
        {
            return;
        }
        // Lua end

        _radarConsole.SetHideTarget((uid, radarConsole), args.Hidden);
        _console.RefreshShuttleConsoles(gridUid);
    }

    // Lua start

    private bool TryGetShuttle(EntityUid uid, out EntityUid gridUid)
    {
        gridUid = EntityUid.Invalid;

        var grid = _tablet.GetTabletGrid(uid) ?? Transform(uid).GridUid;

        if (grid == null)
        {
            return false;
        }

        gridUid = grid.Value;
        return true;
    }

    // Lua end
}
