using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Popups;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.UI.MapObjects;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Content.Server.Worldgen.Components.GC; // Lua
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public const float ShuttleFTLRange = 1500f;
    private const float ShuttleFTLMassThreshold = 50f;

    private bool IsGcAbleGrid(EntityUid gridUid) // Lua start создание проблемы и её героическое решение
    {
        if (HasComp<GCAbleObjectComponent>(gridUid))
            return true;

        var query = AllEntityQuery<GCAbleObjectComponent>();
        while (query.MoveNext(out var comp))
        {
            if (comp.LinkedGridEntity == gridUid)
                return true;
        }

        return false;
    } // Lue end

    private void InitializeFTL()
    {
        SubscribeLocalEvent<FTLBeaconComponent, ComponentStartup>(OnBeaconStartup);
        SubscribeLocalEvent<FTLBeaconComponent, AnchorStateChangedEvent>(OnBeaconAnchorChanged);

        SubscribeLocalEvent<FTLExclusionComponent, ComponentStartup>(OnExclusionStartup);
    }

    private void OnExclusionStartup(Entity<FTLExclusionComponent> ent, ref ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnBeaconStartup(Entity<FTLBeaconComponent> ent, ref ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnBeaconAnchorChanged(Entity<FTLBeaconComponent> ent, ref AnchorStateChangedEvent args)
    {
        RefreshShuttleConsoles();
    }

    private void OnBeaconFTLMessage(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleFTLBeaconMessage args)
    {
        var beaconEnt = GetEntity(args.Beacon);
        if (!_xformQuery.TryGetComponent(beaconEnt, out var targetXform))
        {
            return;
        }

        var nCoordinates = new NetCoordinates(GetNetEntity(targetXform.ParentUid), targetXform.LocalPosition);
        if (targetXform.ParentUid == EntityUid.Invalid)
        {
            nCoordinates = new NetCoordinates(GetNetEntity(beaconEnt), targetXform.LocalPosition);
        }

        // Check target exists
        if (!_shuttle.CanFTLBeacon(nCoordinates))
        {
            return;
        }

        var angle = args.Angle.Reduced();
        var targetCoordinates = new EntityCoordinates(targetXform.MapUid!.Value, _transform.GetWorldPosition(targetXform));

        ConsoleFTL(ent, targetCoordinates, angle, targetXform.MapID);
    }

    private void OnPositionFTLMessage(Entity<ShuttleConsoleComponent> entity, ref ShuttleConsoleFTLPositionMessage args)
    {
        var mapUid = _mapSystem.GetMap(args.Coordinates.MapId);

        // If it's beacons only block all position messages.
        if (!Exists(mapUid) || _shuttle.IsBeaconMap(mapUid))
        {
            return;
        }

        var targetCoordinates = new EntityCoordinates(mapUid, args.Coordinates.Position);
        var angle = args.Angle.Reduced();
        ConsoleFTL(entity, targetCoordinates, angle, args.Coordinates.MapId);
    }

    private void GetBeacons(ref List<ShuttleBeaconObject>? beacons)
    {
        var beaconQuery = AllEntityQuery<FTLBeaconComponent>();

        while (beaconQuery.MoveNext(out var destUid, out _))
        {
            var meta = _metaQuery.GetComponent(destUid);
            var name = meta.EntityName;

            if (string.IsNullOrEmpty(name))
                name = Loc.GetString("shuttle-console-unknown");

            // Can't travel to same map (yet)
            var destXform = _xformQuery.GetComponent(destUid);
            beacons ??= new List<ShuttleBeaconObject>();
            beacons.Add(new ShuttleBeaconObject(GetNetEntity(destUid), GetNetCoordinates(destXform.Coordinates), name));
        }
    }

    private void GetExclusions(ref List<ShuttleExclusionObject>? exclusions)
    {
        var query = AllEntityQuery<FTLExclusionComponent, TransformComponent>();

        while (query.MoveNext(out var comp, out var xform))
        {
            if (!comp.Enabled)
                continue;

            exclusions ??= new List<ShuttleExclusionObject>();
            exclusions.Add(new ShuttleExclusionObject(GetNetCoordinates(xform.Coordinates), comp.Range, Loc.GetString("shuttle-console-exclusion")));
        }
    }

    /// <summary>
    /// Handles shuttle console FTLs.
    /// </summary>
    private void ConsoleFTL(Entity<ShuttleConsoleComponent> ent, EntityCoordinates targetCoordinates, Angle targetAngle, MapId targetMap)
    {
        var consoleUid = GetDroneConsole(ent.Owner);

        if (consoleUid == null)
            return;

        var shuttleUid = _xformQuery.GetComponent(consoleUid.Value).GridUid;

        if (shuttleUid == null || !TryComp(shuttleUid.Value, out ShuttleComponent? shuttleComp))
            return;

        if (shuttleComp.Enabled == false)
            return;

        if (!_shuttle.CanFTL(shuttleUid.Value, out var reason))
        {
            PlayDenySound(ent);
            if (!string.IsNullOrEmpty(reason)) _popup.PopupEntity(reason!, ent.Owner, PopupType.Medium);
            UpdateConsoles(shuttleUid.Value); return;
        }

        // Check shuttle can FTL to this target.
        if (!_shuttle.CanFTLTo(shuttleUid.Value, targetMap, ent))
        {
            return;
        }

        List<ShuttleExclusionObject>? exclusions = null;
        GetExclusions(ref exclusions);

        if (!_shuttle.FTLFree(shuttleUid.Value, targetCoordinates, targetAngle, exclusions))
        {
            return;
        }

        if (!TryComp(shuttleUid.Value, out PhysicsComponent? shuttlePhysics))
        {
            return;
        }

        // Check for nearby grids within the FTL safety radius
        var xform = Transform(shuttleUid.Value);
        var bounds = xform.WorldMatrix.TransformBox(Comp<MapGridComponent>(shuttleUid.Value).LocalAABB).Enlarged(ShuttleFTLRange);

        var dockedShuttles = new HashSet<EntityUid>(); // Lua
        _shuttle.GetAllDockedShuttlesIgnoringFTLLock(shuttleUid.Value, dockedShuttles); // Lua
        foreach (var other in _mapManager.FindGridsIntersecting(xform.MapID, bounds))
        {
            if (other.Owner == shuttleUid.Value)
                continue;

            if (dockedShuttles.Contains(other.Owner)) continue; // Lua
            if (IsGcAbleGrid(other.Owner)) continue;

            PlayDenySound(ent);
            _popup.PopupEntity(Loc.GetString("shuttle-ftl-proximity"), ent.Owner, PopupType.Medium);
            UpdateConsoles(shuttleUid.Value);
            return;
        }

        // Client sends the "adjusted" coordinates and we adjust it back to get the actual transform coordinates.
        var adjustedCoordinates = targetCoordinates.Offset(targetAngle.RotateVec(-shuttlePhysics.LocalCenter));

        var tagEv = new FTLTagEvent();
        RaiseLocalEvent(shuttleUid.Value, ref tagEv);

        var ev = new ShuttleConsoleFTLTravelStartEvent(ent.Owner);
        RaiseLocalEvent(ref ev);

        _shuttle.FTLToCoordinates(shuttleUid.Value, shuttleComp, adjustedCoordinates, targetAngle);
    }

    private void UpdateConsoles(EntityUid uid, ShuttleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Update pilot consoles
        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();

        while (query.MoveNext(out var consoleUid, out var console, out var xform))
        {
            if (xform.GridUid != uid)
                continue;

            UpdateConsoleState(consoleUid, console);
        }
    }

    private void UpdateConsoleState(EntityUid uid, ShuttleConsoleComponent component)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }
}

// Lua: deny
partial class ShuttleConsoleSystem
{
    private void PlayDenySound(Entity<ShuttleConsoleComponent> ent)
    {
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg"), ent.Owner);
    }
}
