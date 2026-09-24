using Content.Client.UserInterface;
using Content.Lua.UIKit.Machines;
using Content.Shared._NF.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._NF.Research.UI;

[UsedImplicitly]
public sealed class ResearchConsoleFrontierBoundUserInterface : FactoryBoundUserInterface
{
    [ViewVariables]
    private ILunaResearchConsoleMenu? _consoleMenu;

    private SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    private ISawmill _sawmill = default!;

    private static readonly SoundPathSpecifier UnlockSound = new("/Audio/_NF/Research/unlock.ogg");

    public ResearchConsoleFrontierBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _audioSystem = EntMan.System<SharedAudioSystem>();

        _sawmill = _logManager.GetSawmill("research.console");
        _sawmill.Debug($"ResearchConsoleFrontierBoundUserInterface created for {owner} with key {uiKey}");
    }

    protected override void Open()
    {
        base.Open();

        if (!IoCManager.Instance!.TryResolveType<ILuaMachineUiFactory>(out var factory))
            return;

        var owner = Owner;
        _sawmill.Debug($"Opening UI for {owner}");

        _consoleMenu = factory.CreateResearchConsoleMenu();
        OpenWindow(_consoleMenu.Window);
        _consoleMenu.SetEntity(owner);
        _consoleMenu.OnClose += () => _consoleMenu = null;

        _consoleMenu.OnTechnologyCardPressed += id =>
        {
            try
            {
                _sawmill.Debug($"Sending ConsoleUnlockTechnologyMessage for tech ID: {id}");
                SendMessage(new ConsoleUnlockTechnologyMessage(id));
                _audioSystem.PlayPvs(UnlockSound, owner, AudioParams.Default);
                _sawmill.Info($"Sent unlock message for technology: {id}");
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Error sending technology unlock message for {id}: {ex}");
            }
        };

        _consoleMenu.OnProjectCancelPressed += id => SendMessage(new ConsoleCancelProjectMessage(id));

        _consoleMenu.OnServerButtonPressed += () =>
        {
            _sawmill.Debug("Sending ConsoleServerSelectionMessage");
            SendMessage(new ConsoleServerSelectionMessage());
        };
    }

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);

        if (!args.WasModified<TechnologyPrototype>())
            return;

        if (State is not ResearchConsoleBoundInterfaceState rState)
            return;

        _sawmill.Debug("Reloading prototypes in UI");
        _consoleMenu?.UpdateQueue(rState.ActiveProjects, rState.QueuedProjects, rState.MaxActiveSlots, rState.TechnologyProgress);
        _consoleMenu?.UpdatePanels(rState.Researches);
        _consoleMenu?.UpdateInformationPanel(rState.Points);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ResearchConsoleBoundInterfaceState castState)
        {
            _sawmill.Warning("Received non-ResearchConsoleBoundInterfaceState state");
            return;
        }

        if (_consoleMenu == null)
        {
            _sawmill.Warning("Console menu is null during state update");
            return;
        }

        _sawmill.Debug($"Updating UI state with {castState.Points} points and {castState.Researches.Count} technologies");
        _consoleMenu.SetResearchFaction(castState.ResearchFaction);
        _consoleMenu.UpdateQueue(castState.ActiveProjects, castState.QueuedProjects, castState.MaxActiveSlots, castState.TechnologyProgress);

        if (!ResearchesEqual(_consoleMenu.List, castState.Researches))
        {
            _sawmill.Debug("Technologies list changed, rebuilding graph");
            _consoleMenu.UpdatePanels(castState.Researches);
            _consoleMenu.UpdateInformationPanel(castState.Points, rebuildTiers: true);
        }
        else
        {
            _consoleMenu.SyncProjectVisuals();
            _consoleMenu.UpdateInformationPanel(castState.Points, rebuildTiers: false);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _consoleMenu?.Window.Dispose();
    }

    private static bool ResearchesEqual(
        Dictionary<string, ResearchAvailability> a,
        Dictionary<string, ResearchAvailability> b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a.Count != b.Count)
            return false;

        foreach (var (id, availability) in a)
        {
            if (!b.TryGetValue(id, out var other) || other != availability)
                return false;
        }

        return true;
    }
}
