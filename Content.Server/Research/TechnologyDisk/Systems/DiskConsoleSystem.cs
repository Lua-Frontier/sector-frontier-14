using Content.Server.Research.Disk;
using Content.Server.Research.Systems;
using Content.Server.Research.TechnologyDisk.Components;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Research.TechnologyDisk.Systems;

public sealed class DiskConsoleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DiskConsoleComponent, DiskConsolePrintDiskMessage>(OnPrintDisk);
        SubscribeLocalEvent<DiskConsoleComponent, ResearchServerPointsChangedEvent>(OnPointsChanged);
        SubscribeLocalEvent<DiskConsoleComponent, ResearchRegistrationChangedEvent>(OnRegistrationChanged);
        SubscribeLocalEvent<DiskConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);

        SubscribeLocalEvent<DiskConsolePrintingComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiskConsolePrintingComponent, DiskConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var printing, out var console, out var xform))
        {
            if (printing.FinishTime > _timing.CurTime)
                continue;

            var points = printing.Points;
            var diskProto = printing.DiskPrototype;
            RemComp(uid, printing);

            var disk = Spawn(diskProto, xform.Coordinates);
            if (TryComp<ResearchDiskComponent>(disk, out var researchDisk))
                researchDisk.Points = points;

            UpdateUserInterface(uid, console);
        }
    }

    private void OnPrintDisk(EntityUid uid, DiskConsoleComponent component, DiskConsolePrintDiskMessage args)
    {
        if (HasComp<DiskConsolePrintingComponent>(uid))
            return;

        if (!component.PointDiskAmounts.Contains(args.Points) || args.Points <= 0)
            return;

        if (!_research.TryGetClientServer(uid, out var server, out var serverComp))
            return;

        if (serverComp.Points < args.Points)
            return;

        _research.ModifyServerPoints(server.Value, -args.Points, serverComp);
        _audio.PlayPvs(component.PrintSound, uid);

        var printing = EnsureComp<DiskConsolePrintingComponent>(uid);
        printing.FinishTime = _timing.CurTime + component.PrintDuration;
        printing.Points = args.Points;
        printing.DiskPrototype = ResolveDiskPrototype(component, args.Points);
        UpdateUserInterface(uid, component);
    }

    private void OnPointsChanged(EntityUid uid, DiskConsoleComponent component, ref ResearchServerPointsChangedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnRegistrationChanged(EntityUid uid, DiskConsoleComponent component, ref ResearchRegistrationChangedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnBeforeUiOpen(EntityUid uid, DiskConsoleComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    public void UpdateUserInterface(EntityUid uid, DiskConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        var totalPoints = 0;
        if (_research.TryGetClientServer(uid, out _, out var server))
            totalPoints = server.Points;

        var printingBusy = TryComp<DiskConsolePrintingComponent>(uid, out var printing) &&
                           printing.FinishTime >= _timing.CurTime;

        var options = new List<DiskConsolePointOption>(component.PointDiskAmounts.Count);
        foreach (var amount in component.PointDiskAmounts)
        {
            options.Add(new DiskConsolePointOption(
                amount,
                !printingBusy && totalPoints >= amount));
        }

        var state = new DiskConsoleBoundUserInterfaceState(totalPoints, printingBusy, options);
        _ui.SetUiState(uid, DiskConsoleUiKey.Key, state);
    }

    private void OnShutdown(EntityUid uid, DiskConsolePrintingComponent component, ComponentShutdown args)
    {
        UpdateUserInterface(uid);
    }

    private static EntProtoId ResolveDiskPrototype(DiskConsoleComponent component, int points)
    {
        if (component.PointDiskPrototypes.TryGetValue(points, out var mapped))
            return mapped;

        return component.DiskPrototype;
    }
}
