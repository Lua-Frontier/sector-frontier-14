using Content.Server._Lua.Sectors;
using Content.Server._NF.Shuttles.Components; // Frontier
using Content.Server.Administration.Logs;
using Content.Server.Body.Systems;
using Content.Server.Buckle.Systems;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Systems;
using Content.Server.Stunnable;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Light.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Salvage;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Content.Server._Lua.Shuttles.Systems;
using Content.Server._Lua.Shuttles.Components;

namespace Content.Server.Shuttles.Systems;

[UsedImplicitly]
public sealed partial class ShuttleSystem : SharedShuttleSystem
{
    // Mono
    public const float TileMassMultiplier = 0.5f;

    [Dependency] private readonly IAdminLogManager _logger = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BiomeSystem _biomes = default!;
    [Dependency] private readonly BodySystem _bobby = default!;
    [Dependency] private readonly BuckleSystem _buckle = default!;
    [Dependency] private readonly DamageableSystem _damageSys = default!;
    [Dependency] private readonly DockingSystem _dockSystem = default!;
    [Dependency] private readonly DungeonSystem _dungeon = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!; // Lua magnet
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tags = default!; // Lua magnet
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!; // Lua magnet
    [Dependency] private readonly SharedSalvageSystem _salvage = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StunSystem _stuns = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ThrusterSystem _thruster = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SectorSystem _sectors = default!;
    [Dependency] private readonly ShuttleGridAccessSystem _gridAccess = default!;

    private EntityQuery<BuckleComponent> _buckleQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _buckleQuery = GetEntityQuery<BuckleComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        InitializeFTL();
        InitializeGridFills();
        InitializeIFF();
        InitializeImpact();

        SubscribeGridEvents<ComponentStartup>(OnGridStartup);
        SubscribeGridEvents<ComponentShutdown>(OnGridShutdown);
        SubscribeGridRefEvents<TileFrictionEvent>(OnTileFriction);
        SubscribeGridRefEvents<FTLStartedEvent>(OnFTLStarted);
        SubscribeGridRefEvents<FTLCompletedEvent>(OnFTLCompleted);

        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
        NfInitialize(); // Frontier
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateHyperspace();
    }

    private void OnGridInit(GridInitializeEvent ev)
    {
        if (HasComp<MapComponent>(ev.EntityUid))
            return;

        _gridAccess.InitializeGrid(ev.EntityUid);
        EnsureComp<ImplicitRoofComponent>(ev.EntityUid);
    }

    private void OnGridStartup(EntityUid uid, IShuttleGrid grid, ComponentStartup args)
    {
        if (!HasComp<MapGridComponent>(uid))
            return;

        if (!TryComp(uid, out PhysicsComponent? physicsComponent))
            return;

        if (grid.Enabled)
            Enable(uid, component: physicsComponent, shuttle: grid);

        grid.DampingModifier = grid.BodyModifier;
    }

    public void Toggle(EntityUid uid, IShuttleGrid grid)
    {
        if (!TryComp(uid, out PhysicsComponent? physicsComponent))
            return;

        if (HasComp<PreventGridAnchorChangesComponent>(uid)) // Frontier
            return; // Frontier

        grid.Enabled = !grid.Enabled;

        if (grid.Enabled)
            Enable(uid, component: physicsComponent, shuttle: grid);
        else
            Disable(uid, component: physicsComponent);
    }

    public void Toggle(EntityUid uid)
    {
        if (!_gridAccess.TryGetShuttleGrid(uid, out var grid))
            return;
        Toggle(uid, grid);
    }

    public void Enable(EntityUid uid, FixturesComponent? manager = null, PhysicsComponent? component = null, IShuttleGrid? shuttle = null)
    {
        if (!Resolve(uid, ref manager, ref component, false))
            return;

        if (HasComp<PreventGridAnchorChangesComponent>(uid)) // Frontier
            return; // Frontier

        _physics.SetBodyType(uid, BodyType.Dynamic, manager: manager, body: component);
        _physics.SetBodyStatus(uid, component, BodyStatus.InAir);
        _physics.SetFixedRotation(uid, false, manager: manager, body: component);
    }

    public void Disable(EntityUid uid, FixturesComponent? manager = null, PhysicsComponent? component = null)
    {
        if (!Resolve(uid, ref manager, ref component, false))
            return;

        if (HasComp<PreventGridAnchorChangesComponent>(uid)) // Frontier
            return; // Frontier

        _physics.SetBodyType(uid, BodyType.Static, manager: manager, body: component);
        _physics.SetBodyStatus(uid, component, BodyStatus.OnGround);
        _physics.SetFixedRotation(uid, true, manager: manager, body: component);
    }

    private void OnGridShutdown(EntityUid uid, IShuttleGrid grid, ComponentShutdown args)
    {
        if (Comp<MetaDataComponent>(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        Disable(uid);
    }

    private void OnTileFriction(EntityUid uid, IShuttleGrid grid, ref TileFrictionEvent args)
    {
        args.Modifier *= grid.DampingModifier;
    }

    private void OnFTLStarted(EntityUid uid, IShuttleGrid grid, ref FTLStartedEvent args)
    {
        grid.DampingModifier = 0f;
    }

    private void OnFTLCompleted(EntityUid uid, IShuttleGrid grid, ref FTLCompletedEvent args)
    {
        grid.DampingModifier = grid.BodyModifier;
        HandleMagneticLatchFtlCompleted(uid, grid, ref args); // Lua
    }

    partial void HandleMagneticLatchFtlCompleted(EntityUid uid, IShuttleGrid grid, ref FTLCompletedEvent args);

    private void SubscribeGridEvents<TEvent>(ShuttleGridEventHandler<TEvent> handler) where TEvent : notnull
    {
        SubscribeLocalEvent<ShuttleGridComponent, TEvent>((uid, comp, args) => handler(uid, comp, args));
        SubscribeLocalEvent<StationGridComponent, TEvent>((uid, comp, args) => handler(uid, comp, args));
        SubscribeLocalEvent<EventGridComponent, TEvent>((uid, comp, args) => handler(uid, comp, args));
        SubscribeLocalEvent<ShuttleAiGridComponent, TEvent>((uid, comp, args) => handler(uid, comp, args));
        SubscribeLocalEvent<DebrisGridComponent, TEvent>((uid, comp, args) => handler(uid, comp, args));
        SubscribeLocalEvent<WrecksGridComponent, TEvent>((uid, comp, args) => handler(uid, comp, args));
    }

    private void SubscribeGridRefEvents<TEvent>(ShuttleGridRefEventHandler<TEvent> handler) where TEvent : struct
    {
        SubscribeLocalEvent<ShuttleGridComponent, TEvent>((uid, comp, ref args) => handler(uid, comp, ref args));
        SubscribeLocalEvent<StationGridComponent, TEvent>((uid, comp, ref args) => handler(uid, comp, ref args));
        SubscribeLocalEvent<EventGridComponent, TEvent>((uid, comp, ref args) => handler(uid, comp, ref args));
        SubscribeLocalEvent<ShuttleAiGridComponent, TEvent>((uid, comp, ref args) => handler(uid, comp, ref args));
        SubscribeLocalEvent<DebrisGridComponent, TEvent>((uid, comp, ref args) => handler(uid, comp, ref args));
        SubscribeLocalEvent<WrecksGridComponent, TEvent>((uid, comp, ref args) => handler(uid, comp, ref args));
    }
}
