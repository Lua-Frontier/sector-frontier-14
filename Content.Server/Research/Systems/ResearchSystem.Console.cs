using Content.Server.Power.EntitySystems;
using Content.Server.Research.Components;
using Content.Shared.UserInterface;
using Content.Shared.Research.Components;
using Content.Shared._NF.Research; // Frontier

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    private void InitializeConsole()
    {
        SubscribeLocalEvent<ResearchConsoleComponent, ConsoleUnlockTechnologyMessage>(OnConsoleUnlock);
        SubscribeLocalEvent<ResearchConsoleComponent, ConsoleCancelProjectMessage>(OnConsoleCancelProject);
        SubscribeLocalEvent<ResearchConsoleComponent, BeforeActivatableUIOpenEvent>(OnConsoleBeforeUiOpened);
        SubscribeLocalEvent<ResearchConsoleComponent, ResearchServerPointsChangedEvent>(OnPointsChanged);
        SubscribeLocalEvent<ResearchConsoleComponent, ResearchRegistrationChangedEvent>(OnConsoleRegistrationChanged);
        SubscribeLocalEvent<ResearchConsoleComponent, TechnologyDatabaseModifiedEvent>(OnConsoleDatabaseModified);
        SubscribeLocalEvent<ResearchConsoleComponent, TechnologyDatabaseSynchronizedEvent>(OnConsoleDatabaseSynchronized);
    }

    private void OnConsoleUnlock(EntityUid uid, ResearchConsoleComponent component, ConsoleUnlockTechnologyMessage args)
    {
        _popup.PopupEntity(Loc.GetString("discovery-research-graph-disabled"), args.Actor);
    }

    private void OnConsoleCancelProject(EntityUid uid, ResearchConsoleComponent component, ConsoleCancelProjectMessage args)
    {
        _popup.PopupEntity(Loc.GetString("discovery-research-graph-disabled"), args.Actor);
    }

    private void OnConsoleBeforeUiOpened(EntityUid uid, ResearchConsoleComponent component, BeforeActivatableUIOpenEvent args)
    {
        SyncClientWithServer(uid);
        UpdateConsoleInterface(uid, component);
    }

    private void UpdateConsoleInterface(EntityUid uid, ResearchConsoleComponent? component = null, ResearchClientComponent? clientComponent = null)
    {
        if (!Resolve(uid, ref component, ref clientComponent, false))
            return;

        var points = 0;
        string? researchFaction = null;
        if (TryGetClientServer(uid, out _, out var server, clientComponent))
        {
            points = server.Points;
            researchFaction = server.Faction;
        }

        _uiSystem.SetUiState(uid, ResearchConsoleUiKey.Key,
            new ResearchConsoleBoundInterfaceState(
                points,
                new Dictionary<string, ResearchAvailability>(),
                researchFaction,
                new List<ResearchProjectUiState>(),
                new List<ResearchProjectUiState>(),
                maxActiveSlots: 0,
                new Dictionary<string, int>()));
    }

    private void OnPointsChanged(EntityUid uid, ResearchConsoleComponent component, ref ResearchServerPointsChangedEvent args)
    {
        if (!_uiSystem.IsUiOpen(uid, ResearchConsoleUiKey.Key))
            return;
        UpdateConsoleInterface(uid, component);
    }

    private void OnConsoleRegistrationChanged(EntityUid uid, ResearchConsoleComponent component, ref ResearchRegistrationChangedEvent args)
    {
        SyncClientWithServer(uid);
        UpdateConsoleInterface(uid, component);
    }

    private void OnConsoleDatabaseModified(EntityUid uid, ResearchConsoleComponent component, ref TechnologyDatabaseModifiedEvent args)
    {
        SyncClientWithServer(uid);
        UpdateConsoleInterface(uid, component);
    }

    private void OnConsoleDatabaseSynchronized(EntityUid uid, ResearchConsoleComponent component, ref TechnologyDatabaseSynchronizedEvent args)
    {
    }
}
