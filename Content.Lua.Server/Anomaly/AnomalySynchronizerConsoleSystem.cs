using System.Linq;
using Content.Server.Anomaly;
using Content.Server.Anomaly.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Lua.Shared.Anomaly;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.UserInterface;

namespace Content.Lua.Server.Anomaly;

public sealed class AnomalySynchronizerConsoleSystem : EntitySystem
{
    [Dependency] private readonly AnomalySynchronizerSystem _sync = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalySynchronizerConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AnomalySynchronizerConsoleComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<AnomalySynchronizerConsoleComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<AnomalySynchronizerConsoleComponent, LinkAttemptEvent>(OnLinkAttempt);
        SubscribeLocalEvent<AnomalySynchronizerConsoleComponent, AfterActivatableUIOpenEvent>(OnUiOpen);
        SubscribeLocalEvent<AnomalySynchronizerConsoleComponent, AnomalySyncConnectMessage>(OnConnect);
        SubscribeLocalEvent<AnomalySynchronizerConsoleComponent, AnomalySyncDisconnectMessage>(OnDisconnect);
        SubscribeLocalEvent<AnomalySynchronizerConsoleComponent, AnomalySyncCompressMessage>(OnCompress);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AnomalySynchronizerConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_ui.IsUiOpen(uid, AnomalySynchronizerConsoleUiKey.Key))
                continue;

            comp.UiAccumulator += frameTime;
            if (comp.UiAccumulator < 0.5f)
                continue;

            comp.UiAccumulator = 0;
            Refresh(uid, comp);
        }
    }

    private void OnMapInit(EntityUid uid, AnomalySynchronizerConsoleComponent comp, MapInitEvent args)
    {
        TryResolveSynchronizer(uid, comp, out _);
    }

    private void OnNewLink(EntityUid uid, AnomalySynchronizerConsoleComponent comp, NewLinkEvent args)
    {
        if (args.Source != uid)
            return;

        if (args.SourcePort != (string)comp.SynchronizerPort)
            return;

        if (!HasComp<AnomalySynchronizerComponent>(args.Sink))
            return;

        foreach (var sinkUid in _deviceLink.GetLinkedSinks(uid).ToList())
        {
            if (sinkUid == args.Sink || !HasComp<AnomalySynchronizerComponent>(sinkUid))
                continue;

            _deviceLink.RemoveSinkFromSource(uid, sinkUid);
        }

        comp.Synchronizer = GetNetEntity(args.Sink);
        Refresh(uid, comp);
    }

    private void OnPortDisconnected(EntityUid uid, AnomalySynchronizerConsoleComponent comp, PortDisconnectedEvent args)
    {
        if (args.Port != (string)comp.SynchronizerPort)
            return;

        TryResolveSynchronizer(uid, comp, out _);
        Refresh(uid, comp);
    }

    private void OnLinkAttempt(EntityUid uid, AnomalySynchronizerConsoleComponent comp, ref LinkAttemptEvent args)
    {
        if (args.Source != uid || args.SourcePort != (string)comp.SynchronizerPort)
            return;

        if (!HasComp<AnomalySynchronizerComponent>(args.Sink))
            return;

        var query = EntityQueryEnumerator<AnomalySynchronizerConsoleComponent>();
        while (query.MoveNext(out var other, out var otherComp))
        {
            if (other == uid)
                continue;

            if (!TryResolveSynchronizer(other, otherComp, out var linked) || linked != args.Sink)
                continue;

            args.Cancel();
            return;
        }
    }

    private void OnUiOpen(EntityUid uid, AnomalySynchronizerConsoleComponent comp, AfterActivatableUIOpenEvent args)
    {
        Refresh(uid, comp);
    }

    private void OnConnect(EntityUid uid, AnomalySynchronizerConsoleComponent comp, AnomalySyncConnectMessage args)
    {
        if (!TryResolveSynchronizer(uid, comp, out var sync))
            return;

        _sync.AttachNearby(sync, args.Actor);
        Refresh(uid, comp);
    }

    private void OnDisconnect(EntityUid uid, AnomalySynchronizerConsoleComponent comp, AnomalySyncDisconnectMessage args)
    {
        if (!TryResolveSynchronizer(uid, comp, out var sync))
            return;

        _sync.DisconnectAttached(sync);
        Refresh(uid, comp);
    }

    private void OnCompress(EntityUid uid, AnomalySynchronizerConsoleComponent comp, AnomalySyncCompressMessage args)
    {
        if (!TryResolveSynchronizer(uid, comp, out var sync))
            return;

        _sync.BeginCompression(sync, args.Actor);
        Refresh(uid, comp);
    }

    private void Refresh(EntityUid uid, AnomalySynchronizerConsoleComponent comp)
    {
        var state = new AnomalySynchronizerConsoleState();
        if (TryResolveSynchronizer(uid, comp, out var sync) && _sync.TryCopyStatus(sync, out var status))
        {
            state.Linked = true;
            state.SynchronizerEntity = status.SynchronizerEntity;
            state.SynchronizerName = MetaData(sync).EntityName;
            state.Powered = status.Powered;
            state.HasAnomaly = status.HasAnomaly;
            state.AnomalyEntity = status.AnomalyEntity;
            state.AnomalyName = status.AnomalyName;
            state.SeverityPercent = status.SeverityPercent;
            state.StabilityPercent = status.StabilityPercent;
            state.HealthPercent = status.HealthPercent;
            state.Phase = status.Phase;
            state.Compressing = status.Compressing;
            state.CompressProgress = status.CompressProgress;
            state.CompressDuration = status.CompressDuration;
            state.CanCompress = status.CanCompress;
            state.ScannerText = status.ScannerText;
            state.NextPulseTime = status.NextPulseTime;
            state.HasBattery = status.HasBattery;
            state.BatteryPercent = status.BatteryPercent;
            state.BatteryCharging = status.BatteryCharging;
        }

        _ui.SetUiState(uid, AnomalySynchronizerConsoleUiKey.Key, state);
    }

    private bool TryResolveSynchronizer(
        EntityUid console,
        AnomalySynchronizerConsoleComponent comp,
        out EntityUid synchronizer)
    {
        synchronizer = default;

        foreach (var sinkUid in _deviceLink.GetLinkedSinks(console))
        {
            if (!Exists(sinkUid) || !HasComp<AnomalySynchronizerComponent>(sinkUid))
                continue;

            if (!_deviceLink.GetLinks(console, sinkUid).Any(link => link.source == comp.SynchronizerPort))
                continue;

            comp.Synchronizer = GetNetEntity(sinkUid);
            synchronizer = sinkUid;
            return true;
        }

        comp.Synchronizer = null;
        return false;
    }
}
