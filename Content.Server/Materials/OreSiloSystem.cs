using Content.Server.Pinpointer;
using Content.Shared.IdentityManagement;
using Content.Shared.Materials.OreSilo;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.Materials;

/// <inheritdoc/>
public sealed class OreSiloSystem : SharedOreSiloSystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private const float OreSiloPreloadRangeSquared = 225f; // ~1 screen
    private const float OreSiloPreloadRange = 25f; // sqrt(OreSiloPreloadRangeSquared)

    private readonly HashSet<Entity<OreSiloClientComponent>> _clientLookup = new();
    private readonly HashSet<(NetEntity, string, string)> _clientInformation = new();
    private readonly HashSet<EntityUid> _silosToAdd = new();
    private readonly HashSet<EntityUid> _silosToRemove = new();

    private readonly HashSet<Entity<OreSiloClientComponent>> _nearClientLookup = new();
    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _sessionSiloOverrides = new();
    private readonly HashSet<ICommonSession> _activeSessions = new();
    private readonly List<ICommonSession> _sessionsToCleanup = new();

    public override void Initialize()
    {
        base.Initialize();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected) return;
        CleanupSessionOverrides(e.Session);
    }

    private void CleanupSessionOverrides(ICommonSession session)
    {
        if (!_sessionSiloOverrides.TryGetValue(session, out var overrides)) return;
        foreach (var silo in overrides)
        { _pvsOverride.RemoveSessionOverride(silo, session); }
        _sessionSiloOverrides.Remove(session);
    }

    protected override void UpdateOreSiloUi(Entity<OreSiloComponent> ent)
    {
        if (!_userInterface.IsUiOpen(ent.Owner, OreSiloUiKey.Key))
            return;
        _clientLookup.Clear();
        _clientInformation.Clear();

        var xform = Transform(ent);

        // Sneakily uses override with TComponent parameter
        _entityLookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range, _clientLookup);

        foreach (var client in _clientLookup)
        {
            // don't show already-linked clients.
            if (client.Comp.Silo is not null)
                continue;

            // Don't show clients on the screen if we can't link them.
            if (!CanTransmitMaterials((ent, ent, xform), client))
                continue;

            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client.Owner, onlyName: true);

            var txt = Loc.GetString("ore-silo-ui-nf-itemlist-entry", // Frontier: use NF key
                ("name", name),
                // ("beacon", beacon), // Frontier
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", true));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        // Get all clients of this silo, including those out of range.
        foreach (var client in ent.Comp.Clients)
        {
            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client, onlyName: true);
            var inRange = CanTransmitMaterials((ent, ent, xform), client);

            var txt = Loc.GetString("ore-silo-ui-nf-itemlist-entry", // Frontier: use NF key
                ("name", name),
                // ("beacon", beacon), // Frontier
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", inRange));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        _userInterface.SetUiState(ent.Owner, OreSiloUiKey.Key, new OreSiloBuiState(_clientInformation));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Solving an annoying problem: we need to send the silo to people who are near the silo so that
        // Things don't start wildly mispredicting. We do this as cheaply as possible via grid-based local-pos checks.
        // Sloth okay-ed this in the interim until a better solution comes around.

        _activeSessions.Clear();
        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out _, out var actorComp, out var actorXform))
        {
            _silosToAdd.Clear();
            _silosToRemove.Clear();

            var session = actorComp.PlayerSession;
            _activeSessions.Add(session);
            if (!_sessionSiloOverrides.TryGetValue(session, out var currentOverrides))
            {
                currentOverrides = new HashSet<EntityUid>();
                _sessionSiloOverrides[session] = currentOverrides;
            }

            _nearClientLookup.Clear();
            _entityLookup.GetEntitiesInRange(actorXform.Coordinates, OreSiloPreloadRange, _nearClientLookup);

            foreach (var client in _nearClientLookup)
            {
                if (client.Comp.Silo is null) continue;
                _silosToAdd.Add(client.Comp.Silo.Value);
            }
            foreach (var silo in currentOverrides)
            { if (!_silosToAdd.Contains(silo)) _silosToRemove.Add(silo); }

            foreach (var toRemove in _silosToRemove)
            {
                _pvsOverride.RemoveSessionOverride(toRemove, session);
                currentOverrides.Remove(toRemove);
            }

            foreach (var toAdd in _silosToAdd)
            {
                if (!currentOverrides.Add(toAdd)) continue;
                _pvsOverride.AddSessionOverride(toAdd, session);
            }
        }
        _sessionsToCleanup.Clear();
        foreach (var (session, overrides) in _sessionSiloOverrides)
        {
            if (_activeSessions.Contains(session)) continue;
            foreach (var silo in overrides)
            { _pvsOverride.RemoveSessionOverride(silo, session); }
            _sessionsToCleanup.Add(session);
        }
        foreach (var session in _sessionsToCleanup)
        { _sessionSiloOverrides.Remove(session); }
    }
}
