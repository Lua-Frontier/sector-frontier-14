using Content.Server._Lua.Starmap.Systems;
using Content.Server._NF.GameTicking.Events;
using Content.Server._NF.SectorServices;
using Content.Server._NF.Shipyard.Systems;
using Content.Server._NF.Smuggling.Components;
using Content.Server._NF.Station.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Radio.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared._NF.CCVar;
using Content.Shared._NF.Smuggling.Prototypes;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Shuttles.Components;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Text;

namespace Content.Server._NF.Smuggling;

public sealed class DeadDropSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _mapManager = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedGameTicker _ticker = default!;
    [Dependency] private readonly LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;
    [Dependency] private readonly StationRenameWarpsSystems _stationRenameWarps = default!;
    [Dependency] private readonly StarmapSystem _starmap = default!;
    private ISawmill _sawmill = default!;

    private readonly Queue<EntityUid> _drops = [];

    private const int MaxHintTimeErrorSeconds = 300; // +/- 5 minutes
    private const int MinCluesPerHint = 1;
    private const int MaxCluesPerHint = 2;

    // Temporary values, sane defaults, will be overwritten by CVARs.
    private int _maxDeadDrops = 8;
    private int _maxSimultaneousPods = 5;
    private int _minDeadDropTimeout = 900;
    private int _maxDeadDropTimeout = 5400;
    private int _minDeadDropDistance = 6500;
    private int _maxDeadDropDistance = 8000;
    private int _minDeadDropHints = 3;
    private int _maxDeadDropHints = 5;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeadDropComponent, ComponentStartup>(OnStartup); //TODO: compromise on shutdown if the stat
        SubscribeLocalEvent<DeadDropComponent, GetVerbsEvent<InteractionVerb>>(AddSearchVerb);
        SubscribeLocalEvent<DeadDropComponent, AnchorStateChangedEvent>(OnDeadDropUnanchored);
        SubscribeLocalEvent<StationDeadDropComponent, ComponentStartup>(OnStationStartup);
        SubscribeLocalEvent<StationDeadDropComponent, ComponentShutdown>(OnStationShutdown);
        SubscribeLocalEvent<StationsGeneratedEvent>(OnStationsGenerated);
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGridAddedAssignDeadDrops);
        SubscribeLocalEvent<PotentialDeadDropComponent, ComponentStartup>(OnPotentialDeadDropStartup);
        SubscribeLocalEvent<SectorDeadDropComponent, ComponentInit>(OnSectorDeadDropInit);

        Subs.CVar(_cfg, NFCCVars.SmugglingMaxSimultaneousPods, OnMaxSimultaneousPodsChanged, true);
        Subs.CVar(_cfg, NFCCVars.SmugglingMaxDeadDrops, OnMaxDeadDropsChanged, true); // TODO: handle this better - will not be reflected until next round.
        Subs.CVar(_cfg, NFCCVars.DeadDropMinTimeout, OnMinDeadDropTimeoutChanged, true);
        Subs.CVar(_cfg, NFCCVars.DeadDropMaxTimeout, OnMaxDeadDropTimeoutChanged, true);
        Subs.CVar(_cfg, NFCCVars.DeadDropMinDistance, OnMinDeadDropDistanceChanged, true);
        Subs.CVar(_cfg, NFCCVars.DeadDropMaxDistance, OnMaxDeadDropDistanceChanged, true);
        Subs.CVar(_cfg, NFCCVars.DeadDropMinHints, OnMinDeadDropHintsChanged, true);
        Subs.CVar(_cfg, NFCCVars.DeadDropMaxHints, OnMaxDeadDropHintsChanged, true);

        _sawmill = Logger.GetSawmill("deaddrop");
    }

    private void OnSectorDeadDropInit(EntityUid _, SectorDeadDropComponent component, ComponentInit args)
    {
        component.ReportedEventsThisHour = new(TimeSpan.FromMinutes(60));
    }

    // CVAR setters
    private void OnMaxSimultaneousPodsChanged(int newMax)
    {
        _maxSimultaneousPods = newMax;
    }

    private void OnMinDeadDropTimeoutChanged(int newMax)
    {
        _minDeadDropTimeout = newMax;
        // Change all existing dead drop timeouts
        var minTime = _timing.CurTime + TimeSpan.FromSeconds(_minDeadDropTimeout);
        var query = EntityManager.AllEntityQueryEnumerator<DeadDropComponent>();
        while (query.MoveNext(out var _, out var comp))
        {
            comp.MinimumCoolDown = _minDeadDropTimeout;
            if (comp.NextDrop < minTime)
                comp.NextDrop = minTime;
        }
    }

    private void OnMaxDeadDropTimeoutChanged(int newMax)
    {
        _maxDeadDropTimeout = newMax;
        // Change all existing dead drop timeouts
        var maxTime = _timing.CurTime + TimeSpan.FromSeconds(_maxDeadDropTimeout);
        var query = EntityManager.AllEntityQueryEnumerator<DeadDropComponent>();
        while (query.MoveNext(out var _, out var comp))
        {
            comp.MaximumCoolDown = _maxDeadDropTimeout;
            if (comp.NextDrop > maxTime)
                comp.NextDrop = maxTime;
        }
    }

    private void OnMinDeadDropDistanceChanged(int newMax)
    {
        _minDeadDropDistance = newMax;
        // Change all existing dead drop timeouts
        var query = EntityManager.AllEntityQueryEnumerator<DeadDropComponent>();
        while (query.MoveNext(out var _, out var comp))
        {
            comp.MinimumDistance = _minDeadDropDistance;
        }
    }

    private void OnMaxDeadDropDistanceChanged(int newMax)
    {
        _maxDeadDropDistance = newMax;
        // Change all existing dead drop timeouts
        var query = EntityManager.AllEntityQueryEnumerator<DeadDropComponent>();
        while (query.MoveNext(out var _, out var comp))
        {
            comp.MaximumDistance = _maxDeadDropDistance;
        }
    }

    private void OnMinDeadDropHintsChanged(int newMin)
    {
        _minDeadDropHints = newMin;
    }

    private void OnMaxDeadDropHintsChanged(int newMax)
    {
        _maxDeadDropHints = newMax;
    }

    private void OnMaxDeadDropsChanged(int newMax)
    {
        _maxDeadDrops = newMax;
    }

    // When a dead drop is unanchored, consider it compromised (we don't want people stealing the dead drop generators, these need to exist in public places)
    private void OnDeadDropUnanchored(EntityUid uid, DeadDropComponent comp, AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

        CompromiseDeadDrop(uid, comp);
    }

    // There is some redundancy here - this should ideally run once over all the stations once worldgen is complete
    // Then once on any new stations if/when they're created.
    private void OnStationStartup(EntityUid stationUid, StationDeadDropComponent component, ComponentStartup _)
    {
        EntityUid resolvedStation;
        if (HasComp<StationDataComponent>(stationUid))
        { resolvedStation = stationUid; }
        else
        {
            var station = _station.GetOwningStation(stationUid);
            if (station == null) return;
            resolvedStation = station.Value;
        }
        if (TryComp<SectorDeadDropComponent>(_sectorService.GetServiceEntity(), out var deadDrop))
        { deadDrop.DeadDropStationNames[resolvedStation] = MetaData(resolvedStation).EntityName; }
        TryAssignDeadDropsForStation(resolvedStation, component.MaxDeadDrops);
    }

    private void TryAssignDeadDropsForStation(EntityUid stationUid, int maxDeadDrops)
    {
        if (maxDeadDrops <= 0) return;
        int existing = 0;
        var activeQuery = EntityManager.EntityQueryEnumerator<DeadDropComponent>();
        while (activeQuery.MoveNext(out var ent, out var _))
        {
            var station = _station.GetOwningStation(ent);
            if (station != null && station.Value == stationUid) existing++;
        }
        var toAssign = int.Max(0, maxDeadDrops - existing);
        if (toAssign <= 0) return;
        List<EntityUid> potentials = new();
        var potentialQuery = EntityManager.EntityQueryEnumerator<PotentialDeadDropComponent>();
        while (potentialQuery.MoveNext(out var ent, out var _))
        {
            var station = _station.GetOwningStation(ent);
            if (station == null || station.Value != stationUid) continue;
            if (!TryComp(ent, out TransformComponent? xform) || !xform.Anchored) continue;
            if (HasComp<DeadDropComponent>(ent)) continue;
            potentials.Add(ent);
        }
        if (potentials.Count == 0)
        { _sawmill.Debug($"{MetaData(stationUid).EntityName} has no potential dead drops available for assignment."); return; }
        _random.Shuffle(potentials);
        int assigned = 0;
        var dropList = new StringBuilder();
        foreach (var ent in potentials)
        {
            if (assigned >= toAssign) break;
            AddDeadDrop(ent);
            assigned++;
            if (dropList.Length <= 0) dropList.Append(ent);
            else dropList.Append($", {ent}");
        }
        if (assigned > 0)
        {
            _sawmill.Debug($"{MetaData(stationUid).EntityName} dead drops assigned: {dropList}");
            _sawmill.Debug($"{MetaData(stationUid).EntityName} late dead drops assigned: {assigned}");
        }
    }

    private void OnStationGridAddedAssignDeadDrops(StationGridAddedEvent ev)
    {
        if (!TryComp<StationDeadDropComponent>(ev.GridId, out var stationDrop)) return;
        var station = ev.Station;
        TryAssignDeadDropsForStation(station, stationDrop.MaxDeadDrops);
    }

    private void OnPotentialDeadDropStartup(EntityUid uid, PotentialDeadDropComponent component, ComponentStartup _)
    {
        var station = _station.GetOwningStation(uid);
        if (station == null) return;
        StationDeadDropComponent? stationDropOnStation;
        StationDeadDropComponent? stationDropOnGrid;
        TryComp<StationDeadDropComponent>(station.Value, out stationDropOnStation);
        TryComp<StationDeadDropComponent>(uid, out stationDropOnGrid);
        if (stationDropOnStation == null && stationDropOnGrid == null) return;
        var max = 0;
        if (stationDropOnStation != null) max = stationDropOnStation.MaxDeadDrops;
        else if (stationDropOnGrid != null) max = stationDropOnGrid.MaxDeadDrops;
        if (max <= 0) return;
        if (!TryComp(uid, out TransformComponent? xform) || !xform.Anchored) return;
        if (HasComp<DeadDropComponent>(uid)) return;
        int existing = 0;
        var activeQuery = EntityManager.EntityQueryEnumerator<DeadDropComponent>();
        while (activeQuery.MoveNext(out var ent, out var _))
        {
            var st = _station.GetOwningStation(ent);
            if (st != null && st.Value == station.Value) existing++;
        }
        if (existing >= max) return;
        AddDeadDrop(uid);
        _sawmill.Debug($"{MetaData(station.Value).EntityName} late single dead drop assigned: {uid}");
    }

    // There is some redundancy here - this should ideally run once over all the stations once worldgen is complete
    // Then once on any new stations if/when they're created.
    private void OnStationShutdown(EntityUid stationUid, StationDeadDropComponent component, ComponentShutdown _)
    {
        if (TryComp<SectorDeadDropComponent>(_sectorService.GetServiceEntity(), out var deadDrop))
        {
            deadDrop.DeadDropStationNames.Remove(stationUid);
        }
    }

    public void CompromiseDeadDrop(EntityUid uid, DeadDropComponent _)
    {
        // Remove the dead drop.
        RemComp<DeadDropComponent>(uid);

        var station = _station.GetOwningStation(uid);
        // If station is terminating, or if we aren't on one, nothing to do here.
        if (station == null ||
            !station.Value.Valid ||
            TerminatingOrDeleted(station.Value))
        {
            return;
        }

        //Find a new potential dead drop to spawn.
        var deadDropQuery = EntityManager.EntityQueryEnumerator<PotentialDeadDropComponent>();
        List<(EntityUid ent, PotentialDeadDropComponent comp)> potentialDeadDrops = new();
        while (deadDropQuery.MoveNext(out var ent, out var potentialDeadDrop))
        {
            // This potential dead drop is not on our station
            if (_station.GetOwningStation(ent) != station)
                continue;

            // This item already has an active dead drop, skip it
            if (HasComp<DeadDropComponent>(ent))
                continue;

            potentialDeadDrops.Add((ent, potentialDeadDrop));
        }

        // We have a potential dead drop, spawn an actual one
        if (potentialDeadDrops.Count > 0)
        {
            var item = _random.Pick(potentialDeadDrops);

            // If the item is tearing down, do nothing for now.
            // FIXME: separate sector-wide scheduler?
            if (TerminatingOrDeleted(item.ent))
                return;

            AddDeadDrop(item.ent);
            _sawmill.Debug($"Dead drop at {uid} compromised, new drop at {item.ent}!");
        }
        else
        {
            _sawmill.Warning($"Dead drop at {uid} compromised, no new drop assigned!");
        }
    }

    // Ensures that a given entity is a valid dead drop with the current global settings.
    public void AddDeadDrop(EntityUid entity)
    {
        var deadDrop = EnsureComp<DeadDropComponent>(entity);
        deadDrop.MinimumCoolDown = _minDeadDropTimeout;
        deadDrop.MaximumCoolDown = _maxDeadDropTimeout;
        deadDrop.MinimumDistance = _minDeadDropDistance;
        deadDrop.MaximumDistance = _maxDeadDropDistance;
    }

    private void OnStationsGenerated(StationsGeneratedEvent args)
    {
        _sawmill.Debug("Generating dead drops");
        var stationDropQuery = AllEntityQuery<StationDeadDropComponent>();
        while (stationDropQuery.MoveNext(out var holder, out var stationDeadDrop))
        {
            EntityUid station;
            if (HasComp<StationDataComponent>(holder)) station = holder;
            else
            {
                var own = _station.GetOwningStation(holder);
                if (own is null) continue;
                station = own.Value;
            }

            TryAssignDeadDropsForStation(station, stationDeadDrop.MaxDeadDrops);
        }
        var hintQuery = AllEntityQuery<DeadDropHintComponent>();
        List<EntityUid> allHints = new();
        while (hintQuery.MoveNext(out var ent, out var _))
            allHints.Add(ent);

        _random.Shuffle(allHints);
        var numHints = _random.Next(_minDeadDropHints, _maxDeadDropHints + 1);
        for (int i = 0; i < allHints.Count && i < numHints; i++)
        {
            var ent = allHints[i];
            if (TryComp<PaperComponent>(ent, out var paper))
            {
                var hintString = GenerateRandomHint();
                _paper.SetContent((ent, paper), hintString);
            }
        }

        if (TryComp<SectorDeadDropComponent>(_sectorService.GetServiceEntity(), out var sectorDeadDrop) &&
            _prototypeManager.TryIndex(sectorDeadDrop.FakeDeadDropHints, out var deadDropHints))
        {
            var hintCount = deadDropHints.Values.Count;
            for (int i = numHints; i < allHints.Count; i++)
            {
                var ent = allHints[i];
                var index = _random.Next(0, hintCount);
                var msg = Loc.GetString(deadDropHints.Values[index]);
                if (TryComp<PaperComponent>(ent, out var paper)) _paper.SetContent((ent, paper), msg);
                RemComp<DeadDropHintComponent>(ent);
            }
        }
    }

    private void OnStartup(EntityUid paintingUid, DeadDropComponent component, ComponentStartup _)
    {
        //set up the timing of the first activation
        if (component.NextDrop == null)
            component.NextDrop = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(component.MinimumCoolDown, component.MaximumCoolDown));
    }

    private void AddSearchVerb(EntityUid uid, DeadDropComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Hands == null || _timing.CurTime < component.NextDrop)
            return;

        var xform = Transform(uid);
        var targetCoordinates = xform.Coordinates;

        //here we build our dynamic verb. Using the object's sprite for now to make it more dynamic for the moment.
        InteractionVerb searchVerb = new()
        {
            IconEntity = GetNetEntity(uid),
            Act = () => SendDeadDrop(uid, component, args.User, args.Hands),
            Text = Loc.GetString("deaddrop-search-text"),
            Priority = 3
        };

        args.Verbs.Add(searchVerb);
    }

    //spawning the dead drop.
    private void SendDeadDrop(EntityUid uid, DeadDropComponent component, EntityUid user, HandsComponent hands)
    {
        //simple check to make sure we dont allow multiple activations from a desynced verb window.
        if (_timing.CurTime < component.NextDrop)
            return;

        //relying entirely on shipyard capabilities, including using the shipyard map to spawn the items and ftl to bring em in
        if (_shipyard.ShipyardMap == null)
        {
            _shipyard.SetupShipyardIfNeeded();
            if (_shipyard.ShipyardMap == null)
                return;
        }

        //load whatever grid was specified on the component, either a special dead drop or default
        if (!_map.TryLoadGrid(_shipyard.ShipyardMap.Value, component.DropGrid, out var gridUid))
            return;
        var grid = gridUid.Value;

        //setup the radar properties
        _shuttle.SetIFFColor(grid, component.Color);
        _shuttle.AddIFFFlag(grid, IFFFlags.HideLabel);

        //this is where we set up all the information that FTL is going to need, including a new null entity as a destination target because FTL needs it for reasons?
        //dont ask me im just fulfilling FTL requirements.
        var dropLocation = _random.NextVector2(component.MinimumDistance, component.MaximumDistance);
        var targetMapId = Transform(user).MapID;
        try
        {
            var stars = _starmap.CollectStars();
            if (stars.Count > 0)
            {
                var candidates = new List<MapId>();
                foreach (var star in stars)
                {
                    if (star.Map == MapId.Nullspace) continue;
                    if (!_mapManager.MapExists(star.Map)) continue;
                    candidates.Add(star.Map);
                }
                if (candidates.Count > 0) targetMapId = _random.Pick(candidates);
            }
        }
        catch { }

        //tries to get the map uid, if it fails, it will return which I would assume will make the component try again.
        if (!_mapManager.TryGetMap(targetMapId, out var mapUid))
        {
            return;
        }

        var stationName = Loc.GetString(component.Name);
        var sectorName = MetaData(mapUid.Value).EntityName;

        var meta = EnsureComp<MetaDataComponent>(grid);
        _meta.SetEntityName(grid, stationName, meta);
        List<EntityUid> gridList = [grid];

        _stationRenameWarps.SyncWarpPointsToGrids(gridList, forceAdminOnly: true);

        // Get sector info (with sane defaults if it doesn't exist)
        int maxSimultaneousPods = 5;
        int deadDropsThisHour = 0;
        if (TryComp<SectorDeadDropComponent>(_sectorService.GetServiceEntity(), out var sectorDeadDrop))
        {
            maxSimultaneousPods = _maxSimultaneousPods;
            if (sectorDeadDrop.ReportedEventsThisHour != null)
            {
                deadDropsThisHour = sectorDeadDrop.ReportedEventsThisHour.Count();
                sectorDeadDrop.ReportedEventsThisHour.AddEvent();
            }
        }

        //this will spawn in the latest ship, and delete the oldest one available if the amount of ships exceeds 5.
        if (TryComp<ShuttleComponent>(grid, out var shuttle))
        {
            _shuttle.FTLToCoordinates(grid, shuttle, new EntityCoordinates(mapUid.Value, dropLocation), 0f, 0f, 35f);
            _drops.Enqueue(grid);

            if (_drops.Count > maxSimultaneousPods)
            {
                //removes the first element of the queue
                var entityToRemove = _drops.Dequeue();
                _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{entityToRemove} queued for deletion");
                _linkedLifecycleGrid.UnparentPlayersFromGrid(entityToRemove, true);
            }
        }

        //tattle on the smuggler here, but obfuscate it a bit if possible to just the grid it was summoned from.
        var sender = Transform(user).GridUid ?? uid;

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user)} sent a dead drop to {dropLocation.ToString()} from {ToPrettyString(uid)} at {Transform(uid).Coordinates.ToString()}");

        //reset the timer (needed for the text)
        component.NextDrop = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(component.MinimumCoolDown, component.MaximumCoolDown));

        var hintNextDrop = component.NextDrop.Value - _ticker.RoundStartTimeSpan + TimeSpan.FromSeconds(_random.Next(-MaxHintTimeErrorSeconds, MaxHintTimeErrorSeconds + 1));

        // here we are just building a string for the hint paper so that it looks pretty and RP-like on the paper itself.
        var dropHint = new StringBuilder();
        dropHint.AppendLine(Loc.GetString("deaddrop-hint-pretext"));
        dropHint.AppendLine();
        dropHint.AppendLine(Loc.GetString("deaddrop-hint-sector", ("sector", sectorName)));
        dropHint.AppendLine(dropLocation.ToString());
        dropHint.AppendLine();
        dropHint.AppendLine(Loc.GetString("deaddrop-hint-posttext"));
        dropHint.AppendLine();
        dropHint.AppendLine(Loc.GetString("deaddrop-hint-next-drop", ("time", hintNextDrop.ToString("hh\\:mm") + ":00")));

        var paper = EntityManager.SpawnEntity(component.HintPaper, Transform(uid).Coordinates);

        if (TryComp(paper, out PaperComponent? paperComp))
        {
            _paper.SetContent((paper, paperComp), dropHint.ToString());
        }
        _meta.SetEntityName(paper, Loc.GetString("deaddrop-hint-name"));
        _meta.SetEntityDescription(paper, Loc.GetString("deaddrop-hint-desc"));
        _hands.PickupOrDrop(user, paper, handsComp: hands);

        component.DeadDropCalled = true;
        //logic of posters ends here and logic of radio signals begins here

        var deadDropQuery = EntityManager.EntityQueryEnumerator<StationDeadDropReportingComponent>();
        while (deadDropQuery.MoveNext(out var reportStation, out var reportComp))
        {
            if (!TryComp<StationDataComponent>(reportStation, out var stationData))
                continue; // Not a station?

            var stationGrid = _station.GetLargestGrid(stationData);
            if (stationGrid == null)
                continue; // Nobody to send our message.

            if (!_prototypeManager.TryIndex(reportComp.MessageSet, out var messageSets))
                continue;

            foreach (var messageSet in messageSets.MessageSets)
            {
                float delayMinutes;
                if (messageSet.MinDelay >= messageSet.MaxDelay)
                    delayMinutes = messageSet.MinDelay;
                else
                    delayMinutes = _random.NextFloat(messageSet.MinDelay, messageSet.MaxDelay);

                if (!_random.Prob(messageSet.Probability))
                    continue;

                string messageLoc = "";
                SmugglingReportMessageType messageType = SmugglingReportMessageType.General;
                float messageError = 0.0f;
                foreach (var message in messageSet.Messages)
                {
                    if (deadDropsThisHour < message.HourlyThreshold)
                    {
                        messageLoc = message.Message;
                        messageType = message.Type;
                        messageError = message.MaxPodLocationError;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(messageLoc))
                    continue;

                string output;
                switch (messageType)
                {
                    case SmugglingReportMessageType.General:
                    default:
                        output = Loc.GetString(messageLoc);
                        break;
                    case SmugglingReportMessageType.DeadDropStation:
                        output = Loc.GetString(messageLoc, ("location", MetaData(sender).EntityName));
                        break;
                    case SmugglingReportMessageType.DeadDropStationWithRandomAlt:
                        var actualStationName = MetaData(sender).EntityName;
                        if (sectorDeadDrop is not null)
                        {
                            var otherStationList = sectorDeadDrop.DeadDropStationNames.Values.Where(x => x != actualStationName).ToList();
                            if (otherStationList.Count > 0)
                            {
                                string[] names = [actualStationName, _random.Pick<string>(otherStationList)];
                                _random.Shuffle(names);
                                output = Loc.GetString(messageLoc, ("location1", names[0]), ("location2", names[1]));
                            }
                            else
                            {
                                // No valid alternate, just output where the dead drop is
                                output = Loc.GetString(messageLoc, ("location1", actualStationName));
                            }
                        }
                        else
                        {
                            // No valid alternate, just output where the dead drop is
                            output = Loc.GetString(messageLoc, ("location1", actualStationName));
                        }
                        break;
                    case SmugglingReportMessageType.PodLocation:
                        var error = _random.NextVector2(messageError);
                        output = Loc.GetString(messageLoc, ("x", $"{dropLocation.X + error.X:F0}"), ("y", $"{dropLocation.Y + error.Y:F0}"));
                        break;
                }

                if (delayMinutes > 0)
                {
                    Timer.Spawn(TimeSpan.FromMinutes(delayMinutes), () =>
                    {
                        _radio.SendRadioMessage(stationGrid.Value, output, messageSets.Channel, uid);
                    });
                }
                else
                {
                    _radio.SendRadioMessage(stationGrid.Value, output, messageSets.Channel, uid);
                }
            }
        }
    }

    // Generates a random hint from a given set of entities (grabs the first N, N randomly generated between min/max),
    public string GenerateRandomHint(List<(EntityUid station, EntityUid ent)>? entityList = null)
    {
        if (entityList == null)
        {
            entityList = new();
            var hintQuery = EntityManager.AllEntityQueryEnumerator<DeadDropComponent>();
            while (hintQuery.MoveNext(out var ent, out var _))
            {
                var stationUid = _station.GetOwningStation(ent);
                if (stationUid != null)
                    entityList.Add((stationUid.Value, ent));
            }
        }

        _random.Shuffle(entityList);

        int hintCount = _random.Next(MinCluesPerHint, MaxCluesPerHint + 1);

        var hintLines = new StringBuilder();
        var hints = 0;
        foreach (var hintTuple in entityList)
        {
            if (hints >= hintCount)
                break;

            string objectHintString;
            if (EntityManager.TryGetComponent<PotentialDeadDropComponent>(hintTuple.Item2, out var potentialDeadDrop))
                objectHintString = Loc.GetString(potentialDeadDrop.HintText);
            else
                objectHintString = Loc.GetString("dead-drop-hint-generic");

            string stationHintString;
            if (EntityManager.TryGetComponent(hintTuple.Item1, out MetaDataComponent? stationMetadata))
                stationHintString = stationMetadata.EntityName;
            else
                stationHintString = Loc.GetString("dead-drop-station-hint-generic");

            string timeString;
            if (EntityManager.TryGetComponent<DeadDropComponent>(hintTuple.Item2, out var deadDrop) && deadDrop.NextDrop != null)
            {
                var dropTimeWithError = deadDrop.NextDrop.Value - _ticker.RoundStartTimeSpan + TimeSpan.FromSeconds(_random.Next(-MaxHintTimeErrorSeconds, MaxHintTimeErrorSeconds));
                timeString = Loc.GetString("dead-drop-time-known", ("time", dropTimeWithError.ToString("hh\\:mm") + ":00"));
            }
            else
            {
                timeString = Loc.GetString("dead-drop-time-unknown");
            }

            hintLines.AppendLine(Loc.GetString("dead-drop-hint-line", ("object", objectHintString), ("poi", stationHintString), ("time", timeString)));
            hints++;
        }
        return Loc.GetString("dead-drop-hint-note", ("drops", hintLines));
    }
}
