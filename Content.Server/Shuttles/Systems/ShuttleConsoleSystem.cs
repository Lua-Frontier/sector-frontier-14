using Content.Server._Mono.Ships.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Systems;
using Content.Shared._Lua.Starmap;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Shuttles.Events; // Frontier
using Content.Shared.Access.Systems; // Frontier
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Construction.Components; // Frontier
using Content.Shared.Lua.CLVar; // Lua
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Shuttles.UI.MapObjects;
using Content.Shared.Tag;
using Content.Shared.Timing;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Collections;
using Robust.Shared.Configuration; // Lua
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;// Lua add timer panic button
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem : SharedShuttleConsoleSystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContentEyeSystem _eyeSystem = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!; // Lua
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly CrewedShuttleSystem _crewedShuttle = default!;

    private ISawmill _sawmill = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private const int PanicConfirmTimeout = 10000;// Lua add timer panic button
    private readonly HashSet<EntityUid> _pendingPanicConfirm = new();// Lua add timer panic button

    private readonly HashSet<Entity<ShuttleConsoleComponent>> _consoles = new();
    private readonly HashSet<EntityUid> _starMapVisibleConsoles = new();

    private static readonly ProtoId<TagPrototype> CanPilotTag = "CanPilot";

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        _sawmill = _log.GetSawmill("shuttle-console");

        InitializeDeviceLinking(); // Initialize device linking functionality

        SubscribeLocalEvent<ShuttleConsoleComponent, ComponentStartup>(OnConsoleStartup);
        SubscribeLocalEvent<ShuttleConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<ShuttleConsoleComponent, PowerChangedEvent>(OnConsolePowerChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, AnchorStateChangedEvent>(OnConsoleAnchorChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, ActivatableUIOpenAttemptEvent>(OnConsoleUIOpenAttempt);
        SubscribeLocalEvent<ShuttleConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnConsoleGetVerbs); //Lua
        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<ShuttleConsoleFTLBeaconMessage>(OnBeaconFTLMessage);
            subs.Event<ShuttleConsoleFTLPositionMessage>(OnPositionFTLMessage);
            subs.Event<ToggleFTLLockRequestMessage>(OnToggleFTLLock);
            subs.Event<WarpToStarMessage>(OnWarpToStarMessage); // Lua
            subs.Event<ShuttleConsoleStarMapVisibilityMessage>(OnStarMapVisibilityMessage); // Lua
            subs.Event<BoundUIClosedEvent>(OnConsoleUIClose);
        });

        SubscribeLocalEvent<DroneConsoleComponent, ConsoleShuttleEvent>(OnCargoGetConsole);
        SubscribeLocalEvent<DroneConsoleComponent, AfterActivatableUIOpenEvent>(OnDronePilotConsoleOpen);
        SubscribeLocalEvent<ShuttleConsoleComponent, EntInsertedIntoContainerMessage>(OnConsoleDiskInserted); // Lua
        SubscribeLocalEvent<ShuttleConsoleComponent, EntRemovedFromContainerMessage>(OnConsoleDiskRemoved); // Lua
        Subs.BuiEvents<DroneConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnDronePilotConsoleClose);
        });

        SubscribeLocalEvent<DockEvent>(OnDock);
        SubscribeLocalEvent<UndockEvent>(OnUndock);

        SubscribeLocalEvent<PilotComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<PilotComponent, StopPilotingAlertEvent>(OnStopPilotingAlert);

        SubscribeLocalEvent<FTLDestinationComponent, ComponentStartup>(OnFtlDestStartup);
        SubscribeLocalEvent<FTLDestinationComponent, ComponentShutdown>(OnFtlDestShutdown);

        InitializeFTL();

        InitializeNFDrone(); // Frontier: add our drone subscriptions

        Subs.CVar(_cfg, CLVars.AutoDelteEnabled, value => _autoDeleteEnabled = value, true); // Lua
    }

    private void OnStarMapVisibilityMessage(EntityUid uid, ShuttleConsoleComponent component, ShuttleConsoleStarMapVisibilityMessage args)
    {
        if (args.Visible)
        {
            _starMapVisibleConsoles.Add(uid);
            DockingInterfaceState? dockState = null;
            UpdateState(uid, ref dockState);
        }
        else
        { _starMapVisibleConsoles.Remove(uid); }
    }

    private bool _autoDeleteEnabled = true; // Lua

    private void OnConsoleGetVerbs(EntityUid uid, ShuttleConsoleComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        AddPanicButtonVerb(uid, comp, args);
        AddPreventRemoverVerb(uid, comp, args);
    }

    private void OnFtlDestStartup(EntityUid uid, FTLDestinationComponent component, ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnFtlDestShutdown(EntityUid uid, FTLDestinationComponent component, ComponentShutdown args)
    {
        RefreshShuttleConsoles();
    }

    private void OnDock(DockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    private void OnUndock(UndockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    /// <summary>
    /// Refreshes all the shuttle console data for a particular grid.
    /// </summary>
    public void RefreshShuttleConsoles(EntityUid gridUid)
    {
        var exclusions = new List<ShuttleExclusionObject>();
        GetExclusions(ref exclusions);
        _consoles.Clear();
        _lookup.GetChildEntities(gridUid, _consoles);
        DockingInterfaceState? dockState = null;

        foreach (var entity in _consoles)
        {
            UpdateState(entity, ref dockState);
        }
    }

    /// <summary>
    /// Refreshes all of the data for shuttle consoles.
    /// </summary>
    public void RefreshShuttleConsoles()
    {
        var exclusions = new List<ShuttleExclusionObject>();
        GetExclusions(ref exclusions);
        var query = AllEntityQuery<ShuttleConsoleComponent>();
        DockingInterfaceState? dockState = null;

        while (query.MoveNext(out var uid, out _))
        {
            UpdateState(uid, ref dockState);
        }
    }

    public void RefreshStarMapForOpenConsoles()
    {
        if (_starMapVisibleConsoles.Count == 0) return;
        DockingInterfaceState? dockState = null;
        foreach (var uid in _starMapVisibleConsoles.ToArray())
        {
            if (Deleted(uid) || !TryComp<ShuttleConsoleComponent>(uid, out _)) continue;
            UpdateState(uid, ref dockState);
        }
    }

    /// <summary>
    /// Stop piloting if the window is closed.
    /// </summary>
    private void OnConsoleUIClose(EntityUid uid, ShuttleConsoleComponent component, BoundUIClosedEvent args)
    {
        if ((ShuttleConsoleUiKey)args.UiKey != ShuttleConsoleUiKey.Key)
        {
            return;
        }

        RemovePilot(args.Actor);
    }

    private void OnConsoleUIOpenAttempt(
        EntityUid uid,
        ShuttleConsoleComponent component,
        ActivatableUIOpenAttemptEvent args)
    {
        var grid = Transform(uid).GridUid;
        var uiOpen = grid != null && _crewedShuttle.AnyGunneryConsoleActiveOnGridByPlayer(grid.Value, args.User);

        if (uiOpen)
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("shuttle-console-crewed"), uid, args.User);
            return;
        }

        if (!TryPilot(args.User, uid))
            args.Cancel();
    }

    private void OnConsoleAnchorChange(EntityUid uid, ShuttleConsoleComponent component,
        ref AnchorStateChangedEvent args)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }

    private void OnConsolePowerChange(EntityUid uid, ShuttleConsoleComponent component, ref PowerChangedEvent args)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
        _shuttle.NfSetPowered(uid, component, args.Powered); // Frontier
    }

    private bool TryPilot(EntityUid user, EntityUid uid)
    {
        if (!_tags.HasTag(user, CanPilotTag) ||
            !TryComp<ShuttleConsoleComponent>(uid, out var component) ||
            !this.IsPowered(uid, EntityManager) ||
            !Transform(uid).Anchored ||
            !_blocker.CanInteract(user, uid))
        {
            return false;
        }

        if (!_access.IsAllowed(user, uid)) // Frontier: check access
            return false; // Frontier

        // Check if console is locked
        if (TryComp<ShuttleConsoleLockComponent>(uid, out var lockComp) && lockComp.Locked)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-console-locked"), uid, user);
            return false;
        }

        var pilotComponent = EnsureComp<PilotComponent>(user);
        var console = pilotComponent.Console;

        if (console != null)
        {
            RemovePilot(user, pilotComponent);

            // This feels backwards; is this intended to be a toggle?
            if (console == uid)
                return false;
        }

        AddPilot(uid, user, component);
        return true;
    }

    private void OnGetState(EntityUid uid, PilotComponent component, ref ComponentGetState args)
    {
        NetEntity consoleNet = NetEntity.Invalid;
        if (component.Console != null && TryGetNetEntity(component.Console.Value, out NetEntity? consoleNetMaybe)) consoleNet = consoleNetMaybe.Value;
        args.State = new PilotComponentState(consoleNet);
    }

    private void OnStopPilotingAlert(Entity<PilotComponent> ent, ref StopPilotingAlertEvent args)
    {
        if (ent.Comp.Console != null)
        {
            RemovePilot(ent.Owner, ent.Comp);
        }
    }

    /// <summary>
    /// Handles FTL lock toggling for docked shuttles
    /// </summary>
    private void OnToggleFTLLock(EntityUid uid, ShuttleConsoleComponent component, ToggleFTLLockRequestMessage args)
    {
        // Get the console's grid (shuttle)
        var consoleXform = Transform(uid);
        var shuttleGrid = consoleXform.GridUid;

        Logger.DebugS("shuttle", $"Server received FTL lock request with {args.DockedEntities.Count} entities, enabled={args.Enabled}");

        // If the shuttleGrid is null, we can't do anything
        if (shuttleGrid == null)
        {
            Logger.DebugS("shuttle", $"Cannot toggle FTL lock: console {ToPrettyString(uid)} is not on a grid");
            return;
        }

        bool processedMainGrid = false;

        // Process each entity in the request
        foreach (var dockedEntityNet in args.DockedEntities)
        {
            var dockedEntity = GetEntity(dockedEntityNet);

            // Check if this is the main shuttle grid
            if (dockedEntity == shuttleGrid)
            {
                processedMainGrid = true;
            }

            if (TryComp<FTLLockComponent>(dockedEntity, out var ftlLock))
            {
                Logger.DebugS("shuttle", $"Setting FTL lock for {ToPrettyString(dockedEntity)} to {args.Enabled}");
                ftlLock.Enabled = args.Enabled;
                Dirty(dockedEntity, ftlLock);
            }
        }

        // If we didn't process the main grid yet, do it now
        if (!processedMainGrid && shuttleGrid != null)
        {
            if (TryComp<FTLLockComponent>(shuttleGrid, out var ftlLock))
            {
                Logger.DebugS("shuttle", $"Setting FTL lock for main grid {ToPrettyString(shuttleGrid.Value)} to {args.Enabled}");
                ftlLock.Enabled = args.Enabled;
                Dirty(shuttleGrid.Value, ftlLock);
            }
        }
    }

    /// <summary>
    /// Sets the FTL lock state of a shuttle entity.
    /// </summary>
    /// <param name="shuttleUid">The shuttle entity to modify</param>
    /// <param name="dockedEntities">List of docked entities to also modify, or empty to only modify the shuttle</param>
    /// <param name="enabled">The desired FTL lock state (true to enable, false to disable)</param>
    /// <returns>True if at least one entity was modified, false otherwise</returns>
    public bool ToggleFTLLock(EntityUid shuttleUid, List<NetEntity> dockedEntities, bool enabled)
    {
        var modified = false;

        // Modify the main shuttle if it has the component
        if (TryComp<FTLLockComponent>(shuttleUid, out var shuttleFtlLock))
        {
            shuttleFtlLock.Enabled = enabled;
            Dirty(shuttleUid, shuttleFtlLock);
            modified = true;
        }

        // Modify any docked entities if provided
        foreach (var dockedEntityNet in dockedEntities)
        {
            var dockedEntity = GetEntity(dockedEntityNet);

            if (TryComp<FTLLockComponent>(dockedEntity, out var ftlLock))
            {
                ftlLock.Enabled = enabled;
                Dirty(dockedEntity, ftlLock);
                modified = true;
            }
        }

        return modified;
    }

    /// <summary>
    /// Returns the position and angle of all dockingcomponents.
    /// </summary>
    public Dictionary<NetEntity, List<DockingPortState>> GetAllDocks()
    {
        // TODO: NEED TO MAKE SURE THIS UPDATES ON ANCHORING CHANGES!
        var result = new Dictionary<NetEntity, List<DockingPortState>>();
        var query = AllEntityQuery<DockingComponent, TransformComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform, out var metadata))
        {
            if (xform.ParentUid != xform.GridUid)
                continue;

            // Frontier: skip unanchored docks (e.g. portable gaslocks)
            if (HasComp<AnchorableComponent>(uid) && !xform.Anchored)
                continue;
            // End Frontier

            var gridDocks = result.GetOrNew(GetNetEntity(xform.GridUid.Value));

            var state = new DockingPortState()
            {
                Name = metadata.EntityName,
                Coordinates = GetNetCoordinates(xform.Coordinates),
                Angle = xform.LocalRotation,
                Entity = GetNetEntity(uid),
                GridDockedWith =
                    _xformQuery.TryGetComponent(comp.DockedWith, out var otherDockXform) ?
                    GetNetEntity(otherDockXform.GridUid) :
                    null,
                LabelName = comp.Name != null ? Loc.GetString(comp.Name) : null, // Frontier: docking labels
                RadarColor = comp.RadarColor, // Frontier
                HighlightedRadarColor = comp.HighlightedRadarColor, // Frontier
                DockType = comp.DockType, // Frontier
                ReceiveOnly = comp.ReceiveOnly, // Frontier
            };

            gridDocks.Add(state);
        }

        return result;
    }

    private void UpdateState(EntityUid consoleUid, ref DockingInterfaceState? dockState)
    {
        EntityUid? entity = consoleUid;

        var getShuttleEv = new ConsoleShuttleEvent
        {
            Console = entity,
        };

        RaiseLocalEvent(entity.Value, ref getShuttleEv);
        entity = getShuttleEv.Console;

        TryComp(entity, out TransformComponent? consoleXform);
        var shuttleGridUid = consoleXform?.GridUid;

        NavInterfaceState navState;
        ShuttleMapInterfaceState mapState;
        dockState ??= GetDockState();

        if (shuttleGridUid != null && entity != null)
        {
            navState = GetNavState(entity.Value, dockState.Docks);
            mapState = GetMapState(shuttleGridUid.Value);
        }
        else
        {
            navState = new NavInterfaceState(0f, null, null, new Dictionary<NetEntity, List<DockingPortState>>(), InertiaDampeningMode.Dampen, ServiceFlags.None, null, NetEntity.Invalid, true); // Frontier: inertia dampening
            mapState = new ShuttleMapInterfaceState(
                FTLState.Invalid,
                default,
                new List<ShuttleBeaconObject>(),
                new List<ShuttleExclusionObject>());
        }

        if (_ui.HasUi(consoleUid, ShuttleConsoleUiKey.Key))
        {
            var currentMap = consoleXform?.MapID ?? MapId.Nullspace;
            var starMapState = GetStarMapState(currentMap, shuttleGridUid, consoleUid);
            _ui.SetUiState(consoleUid, ShuttleConsoleUiKey.Key, new ShuttleBoundUserInterfaceState(navState, mapState, dockState, starMapState));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toRemove = new ValueList<(EntityUid, PilotComponent)>();
        var query = EntityQueryEnumerator<PilotComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Console == null)
                continue;

            if (!_blocker.CanInteract(uid, comp.Console))
            {
                toRemove.Add((uid, comp));
            }
        }

        foreach (var (uid, comp) in toRemove)
        {
            RemovePilot(uid, comp);
        }
    }

    protected override void HandlePilotShutdown(EntityUid uid, PilotComponent component, ComponentShutdown args)
    {
        base.HandlePilotShutdown(uid, component, args);
        RemovePilot(uid, component);
    }

    private void OnConsoleShutdown(EntityUid uid, ShuttleConsoleComponent component, ComponentShutdown args)
    {
        ClearPilots(component);
    }

    public void AddPilot(EntityUid uid, EntityUid entity, ShuttleConsoleComponent component)
    {
        if (!TryComp(entity, out PilotComponent? pilotComponent)
        || component.SubscribedPilots.Contains(entity))
        {
            return;
        }

        _eyeSystem.SetZoom(entity, component.Zoom, ignoreLimits: true);

        component.SubscribedPilots.Add(entity);

        _alertsSystem.ShowAlert(entity, pilotComponent.PilotingAlert);

        pilotComponent.Console = uid;
        ActionBlockerSystem.UpdateCanMove(entity);
        pilotComponent.Position = Comp<TransformComponent>(entity).Coordinates;
        Dirty(entity, pilotComponent);
    }

    public void RemovePilot(EntityUid pilotUid, PilotComponent pilotComponent)
    {
        var console = pilotComponent.Console;

        if (!TryComp<ShuttleConsoleComponent>(console, out var helm))
            return;

        pilotComponent.Console = null;
        pilotComponent.Position = null;
        _eyeSystem.ResetZoom(pilotUid);

        if (!helm.SubscribedPilots.Remove(pilotUid))
            return;

        _alertsSystem.ClearAlert(pilotUid, pilotComponent.PilotingAlert);

        _popup.PopupEntity(Loc.GetString("shuttle-pilot-end"), pilotUid, pilotUid);

        if (pilotComponent.LifeStage < ComponentLifeStage.Stopping)
            RemComp<PilotComponent>(pilotUid);
    }

    public void RemovePilot(EntityUid entity)
    {
        if (!TryComp(entity, out PilotComponent? pilotComponent))
            return;

        RemovePilot(entity, pilotComponent);
    }

    public void ClearPilots(ShuttleConsoleComponent component)
    {
        var query = GetEntityQuery<PilotComponent>();
        while (component.SubscribedPilots.TryGetValue(0, out var pilot))
        {
            if (query.TryGetComponent(pilot, out var pilotComponent))
                RemovePilot(pilot, pilotComponent);
        }
    }

    /// <summary>
    /// Specific for a particular shuttle.
    /// </summary>
    public NavInterfaceState GetNavState(Entity<RadarConsoleComponent?, TransformComponent?> entity, Dictionary<NetEntity, List<DockingPortState>> docks)
    {
        if (!Resolve(entity, ref entity.Comp1, ref entity.Comp2, false))
            return new NavInterfaceState(SharedRadarConsoleSystem.DefaultMaxRange, null, null, docks, Shared._NF.Shuttles.Events.InertiaDampeningMode.Dampen, ServiceFlags.None, null, NetEntity.Invalid, true); // Frontier: add inertia dampening, target

        // Get port names from the console component if available
        var portNames = new Dictionary<string, string>();
        if (TryComp<ShuttleConsoleComponent>(entity, out var consoleComp))
        {
            portNames = consoleComp.PortNames;
        }

        return GetNavState(
            entity,
            docks,
            entity.Comp2.Coordinates,
            entity.Comp2.LocalRotation,
            portNames);
    }

    public NavInterfaceState GetNavState(
        Entity<RadarConsoleComponent?, TransformComponent?> entity,
        Dictionary<NetEntity, List<DockingPortState>> docks,
        EntityCoordinates coordinates,
        Angle angle,
        Dictionary<string, string>? portNames = null)
    {
        if (!Resolve(entity, ref entity.Comp1, ref entity.Comp2))
        {
            var netCoordsFallback = GetNetCoordinatesSafe(coordinates);
            return new NavInterfaceState(
                SharedRadarConsoleSystem.DefaultMaxRange,
                netCoordsFallback,
                angle,
                docks,
                InertiaDampeningMode.Dampen,
                ServiceFlags.None,
                null,
                NetEntity.Invalid,
                true,
                portNames,
                GetExclusionList()); // Frontier: add inertial dampening, target // Lua add GetExclusionList
        }

        var netCoords = GetNetCoordinatesSafe(coordinates);

        // Target entity may be deleted or lack metadata; resolve it quietly.
        var targetNetEntity = NetEntity.Invalid;
        if (entity.Comp1.TargetEntity != null &&
            TryGetNetEntity(entity.Comp1.TargetEntity.Value, out NetEntity? targetNetMaybe))
        {
            targetNetEntity = targetNetMaybe.Value;
        }

        return new NavInterfaceState(
            entity.Comp1.MaxRange,
            netCoords,
            angle,
            docks,
            _shuttle.NfGetInertiaDampeningMode(entity), // Frontier
            _shuttle.NfGetServiceFlags(entity), // Frontier
            entity.Comp1.Target, // Frontier
            targetNetEntity, // Frontier
            entity.Comp1.HideTarget, // Frontier
            portNames,
            GetExclusionList()); // Lua
    }

    private NetCoordinates GetNetCoordinatesSafe(EntityCoordinates coordinates)
    {
        if (!TryGetNetEntity(coordinates.EntityId, out NetEntity? netEntMaybe) || netEntMaybe == null)
            return new NetCoordinates(NetEntity.Invalid, coordinates.Position);

        return new NetCoordinates(netEntMaybe.Value, coordinates.Position);
    }

    //Lua start
    private List<ShuttleExclusionObject> GetExclusionList()
    {
        List<ShuttleExclusionObject>? exclusions = null;
        GetExclusions(ref exclusions);
        return exclusions ?? new List<ShuttleExclusionObject>();
    }
    //Lua end

    /// <summary>
    /// Global for all shuttles.
    /// </summary>
    /// <returns></returns>
    public DockingInterfaceState GetDockState()
    {
        var docks = GetAllDocks();
        return new DockingInterfaceState(docks);
    }

    /// <summary>
    /// Specific to a particular shuttle.
    /// </summary>
    public ShuttleMapInterfaceState GetMapState(Entity<FTLComponent?> shuttle)
    {
        FTLState ftlState = FTLState.Available;
        StartEndTime stateDuration = default;

        if (Resolve(shuttle, ref shuttle.Comp, false) && shuttle.Comp.LifeStage < ComponentLifeStage.Stopped)
        {
            ftlState = shuttle.Comp.State;
            stateDuration = _shuttle.GetStateTime(shuttle.Comp);
        }

        List<ShuttleBeaconObject>? beacons = null;
        List<ShuttleExclusionObject>? exclusions = null;
        GetBeacons(ref beacons);
        GetExclusions(ref exclusions);

        return new ShuttleMapInterfaceState(
            ftlState,
            stateDuration,
            beacons ?? new List<ShuttleBeaconObject>(),
            exclusions ?? new List<ShuttleExclusionObject>());
    }

    /// <summary>
    /// Adds the panic button verb to the shuttle console
    /// </summary>
    private void AddPanicButtonVerb(EntityUid uid, ShuttleConsoleComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !this.IsPowered(uid, EntityManager))
            return;

        // Don't show the panic button if the console is emergency locked
        if (TryComp<ShuttleConsoleLockComponent>(uid, out var lockComp) && lockComp.EmergencyLocked)
            return;

        // Create the panic button verb
        AlternativeVerb verb = new()
        {
            Act = () => SendPanicSignal(uid, args.User, component),
            Text = Loc.GetString("shuttle-console-panic-button"),
            Priority = 1
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Sends an emergency signal to the NFSD radio channel with the shuttle's name and location
    /// </summary>
    private void SendPanicSignal(EntityUid uid, EntityUid user, ShuttleConsoleComponent component)
    {
        // Lua add timer panic button
        if (!_pendingPanicConfirm.Add(uid))
        {
            _pendingPanicConfirm.Remove(uid);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("shuttle-console-panic-confirm"), uid, user);
            Timer.Spawn(PanicConfirmTimeout, () => _pendingPanicConfirm.Remove(uid));
            return;
        }
        // Lua add timer panic button

        // Get the grid entity
        var transform = Transform(uid);
        if (transform.GridUid is not {} gridUid)
        {
            _popup.PopupEntity(Loc.GetString("shuttle-console-panic-no-grid"), uid, user);
            return;
        }

        // Get grid name
        MetaDataComponent? gridMeta = null;
        if (!Resolve(gridUid, ref gridMeta))
        {
            _popup.PopupEntity(Loc.GetString("shuttle-console-panic-failed"), uid, user);
            return;
        }

        var gridName = gridMeta.EntityName;
        var coordinates = transform.Coordinates;
        var mapCoordinates = _transform.ToMapCoordinates(coordinates);

        // Construct emergency message
        string message = Loc.GetString("shuttle-console-panic-message",
            ("gridName", gridName),
            ("coordinates", $"{mapCoordinates.Position.X:0.0}, {mapCoordinates.Position.Y:0.0}"));

        // Send to NFSD radio channel
        _radioSystem.SendRadioMessage(user, message, "Nfsd", uid);

        // Lock the console in emergency mode
        var lockSystem = EntityManager.EntitySysManager.GetEntitySystem<ShuttleConsoleLockSystem>();
        lockSystem.SetEmergencyLock(uid, true);

        // Show confirmation popup
        _popup.PopupEntity(Loc.GetString("shuttle-console-panic-sent"), uid, user);
    }

    private void AddPreventRemoverVerb(EntityUid console, ShuttleConsoleComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_autoDeleteEnabled)
            return; // Lua

        if (!TryComp<TransformComponent>(console, out var xform) || xform.GridUid == null)
            return;

        var grid = xform.GridUid.Value;
        var towComp = EnsureComp<PreventDeleteComponent>(grid);

        var verb = new AlternativeVerb()
        {
            Text = towComp.Remover
                ? Loc.GetString("shuttle-console-towing-allowed")
                : Loc.GetString("shuttle-console-towing-prohibited"),
            Act = () => TogglePreventRemover(console, args.User),
            Priority = 5
        };
        args.Verbs.Add(verb);
    }

    private void TogglePreventRemover(EntityUid console, EntityUid user)
    {
        if (!TryComp<TransformComponent>(console, out var xform) || xform.GridUid == null)
            return;

        var grid = xform.GridUid.Value;
        var comp = EnsureComp<PreventDeleteComponent>(grid);

        comp.Remover = !comp.Remover;
        Dirty(grid, comp);

        var popup = comp.Remover
            ? Loc.GetString("shuttle-console-towing-now-prohibited")
            : Loc.GetString("shuttle-console-towing-now-allowed");

        _popup.PopupEntity(popup, console, user);
    }
}
