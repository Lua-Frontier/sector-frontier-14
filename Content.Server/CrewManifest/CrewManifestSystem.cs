using Content.Server.Administration;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Mono.Company;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.CrewManifest;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.StationRecords;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.CrewManifest;

public sealed class CrewManifestSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    /// <summary>
    ///     Cached crew manifest entries. The alternative is to outright
    ///     rebuild the crew manifest every time the state is requested:
    ///     this is inefficient.
    /// </summary>
    private readonly Dictionary<EntityUid, Dictionary<ICommonSession, CrewManifestEui>> _openEuis = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<RequestCrewManifestMessage>(OnRequestCrewManifest);

        SubscribeLocalEvent<CrewManifestViewerComponent, BoundUIClosedEvent>(OnBoundUiClose);
        SubscribeLocalEvent<CrewManifestViewerComponent, CrewManifestOpenUiMessage>(OpenEuiFromBui);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        foreach (var (_, euis) in _openEuis)
        {
            foreach (var (_, eui) in euis)
            {
                eui.Close();
            }
        }

        _openEuis.Clear();
    }

    private void OnRequestCrewManifest(RequestCrewManifestMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } sessionCast
            || !_configManager.GetCVar(CCVars.CrewManifestWithoutEntity))
        {
            return;
        }

        OpenEui(GetEntity(message.Id), sessionCast);
    }

    private void OnBoundUiClose(EntityUid uid, CrewManifestViewerComponent component, BoundUIClosedEvent ev)
    {
        if (!Equals(ev.UiKey, component.OwnerKey))
            return;

        var owningStation = _stationSystem.GetOwningStation(uid) ?? uid;
        if (!TryComp(ev.Actor, out ActorComponent? actorComp))
            return;

        CloseEui(owningStation, actorComp.PlayerSession, uid);
    }

    public (string name, CrewManifestEntries? entries) GetCrewManifest(EntityUid station)
    {
        return (string.Empty, BuildFactionManifest());
    }

    public (string name, CrewManifestEntries? entries) GetCrewManifestForViewer(EntityUid station, ICommonSession session)
    {
        return (string.Empty, FilterEntriesForViewer(BuildFactionManifest(), session));
    }

    public (string name, CrewManifestEntries? entries) GetCrewManifestForViewer(EntityUid station, EntityUid viewer)
    {
        var raw = BuildFactionManifest();

        if (TryComp(viewer, out ActorComponent? actor))
            return (string.Empty, FilterEntriesForViewer(raw, actor.PlayerSession));

        return (string.Empty, FilterEntriesForCompany(raw, GetEntityCompanyId(viewer), adminView: false));
    }

    public EntityUid? TryGetLoaderHolder(EntityUid loaderUid)
    {
        if (_container.TryGetContainingContainer((loaderUid, null, null), out var container))
            return container.Owner;

        return null;
    }

    private void UpdateEuis(EntityUid key)
    {
        if (_openEuis.TryGetValue(key, out var euis))
        {
            foreach (var eui in euis.Values)
            {
                eui.StateDirty();
            }
        }

        foreach (var euis2 in _openEuis.Values)
        {
            foreach (var eui in euis2.Values)
            {
                eui.StateDirty();
            }
        }
    }

    private void OpenEuiFromBui(EntityUid uid, CrewManifestViewerComponent component, CrewManifestOpenUiMessage msg)
    {
        if (!msg.UiKey.Equals(component.OwnerKey))
        {
            Log.Error(
                "{User} tried to open crew manifest from wrong UI: {Key}. Correct owned is {ExpectedKey}",
                msg.Actor, msg.UiKey, component.OwnerKey);
            return;
        }

        if (!TryComp(msg.Actor, out ActorComponent? actorComp))
            return;

        if (!_configManager.GetCVar(CCVars.CrewManifestUnsecure) && component.Unsecure)
            return;

        var key = _stationSystem.GetOwningStation(uid) ?? uid;
        OpenEui(key, actorComp.PlayerSession, uid);
    }

    public void OpenEui(EntityUid station, ICommonSession session, EntityUid? owner = null)
    {
        if (!_openEuis.TryGetValue(station, out var euis))
        {
            euis = new();
            _openEuis.Add(station, euis);
        }

        if (euis.ContainsKey(session))
            return;

        var eui = new CrewManifestEui(station, owner, this);
        euis.Add(session, eui);

        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    public void CloseEui(EntityUid station, ICommonSession session, EntityUid? owner = null)
    {
        if (!_openEuis.TryGetValue(station, out var euis)
            || !euis.TryGetValue(session, out var eui))
        {
            return;
        }

        if (eui.Owner == owner)
        {
            euis.Remove(session);
            eui.Close();
        }

        if (euis.Count == 0)
            _openEuis.Remove(station);
    }

    /// <summary>
    ///     Builds the crew manifest for a station. Stores it in the cache afterwards.
    /// </summary>
    private CrewManifestEntries BuildFactionManifest()
    {
        var entriesSort = new List<(JobPrototype? job, CrewManifestEntry entry)>();
        var query = EntityQueryEnumerator<CompanyComponent, MetaDataComponent, MindContainerComponent>();

        while (query.MoveNext(out var uid, out var company, out var meta, out _))
        {
            if (!_mind.TryGetMind(uid, out var mindId, out var mindComp))
                continue;

            var companyId = string.IsNullOrWhiteSpace(company.CompanyName) ? "None" : company.CompanyName;

            string? playerName = null;
            if (TryComp(uid, out ActorComponent? actor))
                playerName = actor.PlayerSession.Name;
            else if (mindComp.UserId != null &&
                     _playerManager.TryGetSessionById(mindComp.UserId.Value, out var session))
                playerName = session.Name;

            _jobs.MindTryGetJob(mindId, out var job);
            var jobTitle = job?.LocalizedName ?? Loc.GetString("generic-unknown-title");
            var jobIcon = job?.Icon ?? "JobIconUnknown";
            var jobProto = job?.ID ?? string.Empty;

            var entry = new CrewManifestEntry(
                meta.EntityName,
                jobTitle,
                jobIcon,
                jobProto,
                companyId,
                playerName);

            entriesSort.Add((job, entry));
        }

        entriesSort.Sort((a, b) =>
        {
            var nameCmp = string.Compare(a.entry.Name, b.entry.Name, StringComparison.CurrentCultureIgnoreCase);
            if (nameCmp != 0)
                return nameCmp;

            return JobUIComparer.Instance.Compare(a.job, b.job);
        });

        return new CrewManifestEntries
        {
            Entries = entriesSort.Select(x => x.entry).ToArray(),
        };
    }

    private CrewManifestEntries FilterEntriesForViewer(CrewManifestEntries raw, ICommonSession session)
    {
        if (IsAdminObserver(session))
            return FilterEntriesForCompany(raw, companyId: null, adminView: true);

        return FilterEntriesForCompany(raw, GetViewerCompanyId(session), adminView: false);
    }

    private CrewManifestEntries FilterEntriesForCompany(CrewManifestEntries raw, string? companyId, bool adminView)
    {
        IEnumerable<CrewManifestEntry> source = raw.Entries;

        if (!adminView)
        {
            var filterId = string.IsNullOrWhiteSpace(companyId) ? "None" : companyId;
            source = source.Where(e =>
                string.Equals(
                    string.IsNullOrWhiteSpace(e.CompanyId) ? "None" : e.CompanyId,
                    filterId,
                    StringComparison.OrdinalIgnoreCase));
        }

        var filtered = source
            .Select(e => adminView
                ? e
                : new CrewManifestEntry(e.Name, e.JobTitle, e.JobIcon, e.JobPrototype, e.CompanyId, playerName: null))
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.CompanyId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CrewManifestEntries
        {
            Entries = filtered,
            GroupByCompany = true,
        };
    }

    private string GetViewerCompanyId(ICommonSession session)
    {
        if (session.AttachedEntity is { } ent)
            return GetEntityCompanyId(ent);

        return "None";
    }

    private string GetEntityCompanyId(EntityUid uid)
    {
        if (TryComp(uid, out CompanyComponent? company) && !string.IsNullOrWhiteSpace(company.CompanyName))
            return company.CompanyName;

        return "None";
    }

    private bool IsAdminObserver(ICommonSession session)
    {
        if (session.AttachedEntity is not { } ent)
            return false;

        var proto = MetaData(ent).EntityPrototype;
        return proto != null && proto.ID == GameTicker.AdminObserverPrototypeName;
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class CrewManifestCommand : LocalizedEntityCommands
{
    [Dependency] private readonly CrewManifestSystem _manifestSystem = default!;

    public override string Command => "crewmanifest";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Loc.GetString($"shell-need-exactly-one-argument"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var uidNet) || !EntityManager.TryGetEntity(uidNet, out var uid))
        {
            shell.WriteLine(Loc.GetString($"shell-argument-station-id-invalid", ("index", args[0])));
            return;
        }

        if (shell.Player is not { } session)
        {
            shell.WriteLine(Loc.GetString($"shell-cannot-run-command-from-server"));
            return;
        }

        _manifestSystem.OpenEui(uid.Value, session);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var stations = new List<CompletionOption>();
        var query = EntityManager.EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var meta = EntityManager.GetComponent<MetaDataComponent>(uid);
            stations.Add(new CompletionOption(uid.ToString(), meta.EntityName));
        }

        return CompletionResult.FromHintOptions(stations, null);
    }
}
