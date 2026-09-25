using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Anomaly.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Shared.Anomaly.Components;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Anomaly;

/// <summary>
/// a device that allows you to translate anomaly activity into multitool signals.
/// </summary>
public sealed partial class AnomalySynchronizerSystem : EntitySystem
{
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly DeviceLinkSystem _signal = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalySynchronizerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AnomalySynchronizerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<AnomalySynchronizerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<AnomalySynchronizerComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
        SubscribeLocalEvent<AnomalySynchronizerComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<AnomalySynchronizerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AnomalySynchronizerComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<AnomalyShutdownEvent>(OnAnomalyShutdown);

        SubscribeLocalEvent<AnomalyPulseEvent>(OnAnomalyPulse);
        SubscribeLocalEvent<AnomalySeverityChangedEvent>(OnAnomalySeverityChanged);
        SubscribeLocalEvent<AnomalyStabilityChangedEvent>(OnAnomalyStabilityChanged);
    }

    private void OnMapInit(Entity<AnomalySynchronizerComponent> ent, ref MapInitEvent args)
    {
        UpdatePowerVisuals(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AnomalySynchronizerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sync, out var xform))
        {
            if (xform.Anchored && _power.IsPowered(uid))
                ChargeBattery((uid, sync), frameTime);

            if (sync.Compressing)
                TickCompression((uid, sync), frameTime);

            if (sync.ConnectedAnomaly is null)
                continue;

            if (_timing.CurTime < sync.NextCheckTime)
                continue;

            var interval = (float)sync.CheckFrequency.TotalSeconds;
            sync.NextCheckTime += sync.CheckFrequency;

            var anomaly = sync.ConnectedAnomaly.Value;
            if (!Exists(anomaly))
            {
                ClearConnection((uid, sync), playSound: false);
                continue;
            }

            if (!IsOperational(uid))
            {
                DisconnectFromAnomaly((uid, sync), anomaly);
                continue;
            }

            if (!sync.Compressing && !TryDrawHoldPower((uid, sync), interval))
            {
                DisconnectFromAnomaly((uid, sync), anomaly);
                continue;
            }

            if (Transform(anomaly).MapUid != Transform(uid).MapUid)
            {
                DisconnectFromAnomaly((uid, sync), anomaly);
                continue;
            }

            HoldAnomaly((uid, sync), anomaly);
        }
    }

    /// <summary>
    /// If powered, try to attach a nearby anomaly.
    /// </summary>
    public bool TryAttachNearbyAnomaly(Entity<AnomalySynchronizerComponent> ent, EntityUid? user = null)
    {
        if (!IsOperational(ent))
        {
            if (user is not null)
                _popup.PopupEntity(Loc.GetString("base-computer-ui-component-not-powered", ("machine", ent)), ent, user.Value);

            return false;
        }

        if (ent.Comp.ConnectedAnomaly is { } already && Exists(already))
            return true;

        var coords = _transform.GetMapCoordinates(ent);
        var anomaly = _entityLookup.GetEntitiesInRange<AnomalyComponent>(coords, ent.Comp.AttachRange).FirstOrDefault();

        if (anomaly.Owner is { Valid: false }) // no anomaly in range
        {
            if (user is not null)
                _popup.PopupEntity(Loc.GetString("anomaly-sync-no-anomaly"), ent, user.Value);

            return false;
        }

        if (IsAnomalyAttached(anomaly) && ent.Comp.ConnectedAnomaly != anomaly.Owner)
        {
            if (user is not null)
                _popup.PopupEntity(Loc.GetString("anomaly-sync-no-anomaly"), ent, user.Value);
            return false;
        }

        ConnectToAnomaly(ent, anomaly);
        return true;
    }

    private void OnPowerChanged(Entity<AnomalySynchronizerComponent> ent, ref PowerChangedEvent args)
    {
        UpdatePowerVisuals(ent);

        if (args.Powered)
            return;

        if (HasBatteryCharge(ent))
            return;

        if (ent.Comp.ConnectedAnomaly is null)
            return;

        DisconnectFromAnomaly(ent, ent.Comp.ConnectedAnomaly.Value);
    }

    private void OnPowerCellEmpty(Entity<AnomalySynchronizerComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        UpdatePowerVisuals(ent);

        if (_power.IsPowered(ent))
            return;

        if (ent.Comp.ConnectedAnomaly is null)
            return;

        DisconnectFromAnomaly(ent, ent.Comp.ConnectedAnomaly.Value);
    }

    private void OnPowerCellChanged(EntityUid uid, AnomalySynchronizerComponent component, PowerCellChangedEvent args)
    {
        UpdatePowerVisuals(uid);

        if (!args.Ejected && IsOperational(uid))
            return;

        if (_power.IsPowered(uid) || HasBatteryCharge(uid))
            return;

        if (component.ConnectedAnomaly is null)
            return;

        DisconnectFromAnomaly((uid, component), component.ConnectedAnomaly.Value);
    }

    private void OnExamined(Entity<AnomalySynchronizerComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.ConnectedAnomaly.HasValue ? "anomaly-sync-examine-connected" : "anomaly-sync-examine-not-connected"));
    }

    private void OnGetInteractionVerbs(Entity<AnomalySynchronizerComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands is null || ent.Comp.ConnectedAnomaly.HasValue)
            return;

        var user = args.User;
        args.Verbs.Add(new()
        {
            Act = () =>
            {
                TryAttachNearbyAnomaly(ent, user);
            },
            Message = Loc.GetString("anomaly-sync-connect-verb-message", ("machine", ent)),
            Text = Loc.GetString("anomaly-sync-connect-verb-text"),
        });
    }

    private void OnInteractHand(Entity<AnomalySynchronizerComponent> ent, ref InteractHandEvent args)
    {
        TryAttachNearbyAnomaly(ent, args.User);
    }

    private void OnAnomalyShutdown(ref AnomalyShutdownEvent args)
    {
        var query = EntityQueryEnumerator<AnomalySynchronizerComponent>();
        while (query.MoveNext(out var uid, out var sync))
        {
            if (sync.ConnectedAnomaly != args.Anomaly)
                continue;

            ClearConnection((uid, sync), playSound: false);
        }
    }

    private void ConnectToAnomaly(Entity<AnomalySynchronizerComponent> ent, Entity<AnomalyComponent> anomaly)
    {
        if (ent.Comp.ConnectedAnomaly == anomaly)
            return;

        ent.Comp.ConnectedAnomaly = anomaly;
        EnsureComp<AnomalySyncHeldComponent>(anomaly);
        HoldAnomaly(ent, anomaly);

        if (ent.Comp.PulseOnConnect)
            _anomaly.DoAnomalyPulse(anomaly, anomaly);

        _popup.PopupEntity(Loc.GetString("anomaly-sync-connected"), ent, PopupType.Medium);
        _audio.PlayPvs(ent.Comp.ConnectedSound, ent);
    }

    private void HoldAnomaly(Entity<AnomalySynchronizerComponent> ent, EntityUid anomaly)
    {
        var anomalyXform = Transform(anomaly);
        if (anomalyXform.ParentUid != ent.Owner)
        {
            _transform.SetParent(anomaly, anomalyXform, ent.Owner);
            anomalyXform = Transform(anomaly);
        }

        if (anomalyXform.LocalPosition != Vector2.Zero)
            _transform.SetLocalPosition(anomaly, Vector2.Zero, anomalyXform);

        if (TryComp<PhysicsComponent>(anomaly, out var physics))
        {
            _physics.SetLinearVelocity(anomaly, Vector2.Zero, body: physics);
            if (physics.BodyType != BodyType.Kinematic)
                _physics.SetBodyType(anomaly, BodyType.Kinematic, body: physics);
        }

        EnsureComp<AnomalySyncHeldComponent>(anomaly);
    }

    private void DisconnectFromAnomaly(Entity<AnomalySynchronizerComponent> ent, EntityUid other)
    {
        if (ent.Comp.ConnectedAnomaly == null)
            return;

        if (Exists(other) && TryComp<AnomalyComponent>(other, out var anomaly) && ent.Comp.PulseOnDisconnect)
            _anomaly.DoAnomalyPulse(other, anomaly);

        ClearConnection(ent, playSound: true);
        _popup.PopupEntity(Loc.GetString("anomaly-sync-disconnected"), ent, PopupType.Large);
    }

    private void ClearConnection(Entity<AnomalySynchronizerComponent> ent, bool playSound)
    {
        if (ent.Comp.ConnectedAnomaly is { } anomaly && Exists(anomaly))
        {
            RemComp<AnomalySyncHeldComponent>(anomaly);
            if (TryComp<PhysicsComponent>(anomaly, out var physics))
                _physics.SetBodyType(anomaly, BodyType.Static, body: physics);
            _transform.AttachToGridOrMap(anomaly);
        }

        StopCompressionVisuals(ent);
        ent.Comp.Compressing = false;
        ent.Comp.CompressProgress = 0;
        ent.Comp.CompressEffectAccumulator = 0;
        ent.Comp.ConnectedAnomaly = null;

        if (playSound)
            _audio.PlayPvs(ent.Comp.DisconnectedSound, ent);
    }

    private void OnAnomalyPulse(ref AnomalyPulseEvent args)
    {
        var query = EntityQueryEnumerator<AnomalySynchronizerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (args.Anomaly != component.ConnectedAnomaly)
                continue;

            if (!IsOperational(uid))
                continue;

            _signal.InvokePort(uid, component.PulsePort);
        }
    }

    private void OnAnomalySeverityChanged(ref AnomalySeverityChangedEvent args)
    {
        var query = EntityQueryEnumerator<AnomalySynchronizerComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            if (args.Anomaly != component.ConnectedAnomaly)
                continue;

            if (!IsOperational(ent))
                continue;

            //The superscritical port is invoked not at the AnomalySupercriticalEvent,
            //but at the moment the growth animation starts. Otherwise, there is no point in this port.
            //ATTENTION! the console command supercriticalanomaly does not work here,
            //as it forcefully causes growth to start without increasing severity.
            if (args.Severity >= 1)
                _signal.InvokePort(ent, component.SupercritPort);
        }
    }

    private void OnAnomalyStabilityChanged(ref AnomalyStabilityChangedEvent args)
    {
        Entity<AnomalyComponent> anomaly = (args.Anomaly, Comp<AnomalyComponent>(args.Anomaly));

        var query = EntityQueryEnumerator<AnomalySynchronizerComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            if (component.ConnectedAnomaly != anomaly)
                continue;

            if (!IsOperational(ent))
                continue;

            if (args.Stability < anomaly.Comp.DecayThreshold)
            {
                _signal.InvokePort(ent, component.DecayingPort);
            }
            else if (args.Stability > anomaly.Comp.GrowthThreshold)
            {
                _signal.InvokePort(ent, component.GrowingPort);
            }
            else
            {
                _signal.InvokePort(ent, component.StabilizePort);
            }
        }
    }

    public bool IsAnomalyAttached(EntityUid anomaly)
    {
        var query = EntityQueryEnumerator<AnomalySynchronizerComponent>();
        while (query.MoveNext(out _, out var sync))
        {
            if (sync.ConnectedAnomaly == anomaly)
                return true;
        }

        return false;
    }

    public bool TryGetSynchronizer(EntityUid uid, [NotNullWhen(true)] out AnomalySynchronizerComponent? component)
    {
        return TryComp(uid, out component);
    }

    public bool AttachNearby(EntityUid uid, EntityUid? user)
    {
        return TryComp<AnomalySynchronizerComponent>(uid, out var comp) && TryAttachNearbyAnomaly((uid, comp), user);
    }

    public void DisconnectAttached(EntityUid uid)
    {
        if (!TryComp<AnomalySynchronizerComponent>(uid, out var comp) || comp.ConnectedAnomaly is not { } anomaly)
            return;

        DisconnectFromAnomaly((uid, comp), anomaly);
    }

    public bool BeginCompression(EntityUid uid, EntityUid? user)
    {
        return TryComp<AnomalySynchronizerComponent>(uid, out var comp) && TryStartCompression(uid, comp, user);
    }

    public void Disconnect(EntityUid uid, AnomalySynchronizerComponent component)
    {
        if (component.ConnectedAnomaly is not { } anomaly)
            return;

        DisconnectFromAnomaly((uid, component), anomaly);
    }

    public bool TryStartCompression(EntityUid uid, AnomalySynchronizerComponent component, EntityUid? user)
    {
        if (component.Compressing)
            return false;

        if (!IsOperational(uid))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("base-computer-ui-component-not-powered", ("machine", uid)), uid, user.Value);
            return false;
        }

        if (component.ConnectedAnomaly is not { } anomaly || !TryComp<AnomalyComponent>(anomaly, out var anomalyComp))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("anomaly-sync-no-anomaly"), uid, user.Value);
            return false;
        }

        if (anomalyComp.CorePrototype == null)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("anomaly-sync-compress-no-core"), uid, user.Value);
            return false;
        }

        component.Compressing = true;
        component.CompressProgress = 0;
        component.CompressEffectAccumulator = 0;

        var held = EnsureComp<AnomalySyncHeldComponent>(anomaly);
        held.Compressing = true;
        Dirty(anomaly, held);

        _ambient.SetAmbience(uid, true);
        _audio.PlayPvs(component.ConnectedSound, uid);
        _popup.PopupEntity(Loc.GetString("anomaly-sync-compress-start"), uid, PopupType.Medium);
        return true;
    }

    private void TickCompression(Entity<AnomalySynchronizerComponent> ent, float frameTime)
    {
        if (ent.Comp.ConnectedAnomaly is not { } anomaly || !Exists(anomaly) || !IsOperational(ent))
        {
            AbortCompression(ent);
            return;
        }

        if (!TryDrawHoldPower(ent, frameTime, compressing: true))
        {
            AbortCompression(ent);
            DisconnectFromAnomaly(ent, anomaly);
            return;
        }
        HoldAnomaly(ent, anomaly);
        ent.Comp.CompressProgress += frameTime;
        ent.Comp.CompressEffectAccumulator += frameTime;
        if (ent.Comp.CompressEffectAccumulator >= ent.Comp.CompressEffectInterval)
        {
            ent.Comp.CompressEffectAccumulator = 0;
            Spawn(ent.Comp.CompressLoopEffect, Transform(anomaly).Coordinates);
        }
        if (ent.Comp.CompressProgress < ent.Comp.CompressDuration)
            return;
        if (!_anomaly.TryCompressIntoCore(anomaly))
        {
            AbortCompression(ent);
            _popup.PopupEntity(Loc.GetString("anomaly-sync-compress-no-core"), ent, PopupType.Medium);
            return;
        }
        _audio.PlayPvs(ent.Comp.CompressCompleteSound, ent);
        _popup.PopupEntity(Loc.GetString("anomaly-sync-compress-complete"), ent, PopupType.Large);
        ClearConnection(ent, playSound: false);
    }

    private void AbortCompression(Entity<AnomalySynchronizerComponent> ent)
    {
        if (ent.Comp.ConnectedAnomaly is { } anomaly && Exists(anomaly) && TryComp<AnomalySyncHeldComponent>(anomaly, out var held))
        {
            held.Compressing = false;
            Dirty(anomaly, held);
        }

        StopCompressionVisuals(ent);
        ent.Comp.Compressing = false;
        ent.Comp.CompressProgress = 0;
        ent.Comp.CompressEffectAccumulator = 0;
    }

    private void StopCompressionVisuals(Entity<AnomalySynchronizerComponent> ent)
    {
        _ambient.SetAmbience(ent, false);
    }

    public bool TryCopyStatus(EntityUid uid, out AnomalySyncStatus status)
    {
        status = new AnomalySyncStatus();
        if (!TryComp<AnomalySynchronizerComponent>(uid, out var sync))
            return false;

        status.SynchronizerEntity = GetNetEntity(uid);
        status.Powered = IsOperational(uid);
        status.Compressing = sync.Compressing;
        status.CompressProgress = sync.CompressProgress;
        status.CompressDuration = sync.CompressDuration;
        status.HasAnomaly = sync.ConnectedAnomaly is { } connected && Exists(connected);
        CopyBatteryStatus(uid, status);

        if (sync.ConnectedAnomaly is { } anomaly && TryComp<AnomalyComponent>(anomaly, out var anomalyComp))
        {
            status.AnomalyEntity = GetNetEntity(anomaly);
            status.AnomalyName = MetaData(anomaly).EntityName;
            status.SeverityPercent = (int)MathF.Round(anomalyComp.Severity * 100f);
            status.StabilityPercent = (int)MathF.Round(anomalyComp.Stability * 100f);
            status.HealthPercent = (int)MathF.Round(anomalyComp.Health * 100f);
            status.Phase = anomalyComp.Stability < anomalyComp.DecayThreshold
                ? "decaying"
                : anomalyComp.Stability > anomalyComp.GrowthThreshold
                    ? "growing"
                    : "stable";
            status.CanCompress = status.Powered && !sync.Compressing && anomalyComp.CorePrototype != null;
            status.ScannerText = _anomaly.GetScannerMessageForAnomaly(anomaly).ToMarkup();
            status.NextPulseTime = anomalyComp.NextPulseTime;
        }

        return true;
    }
    private bool IsOperational(EntityUid uid)
    {
        if (_power.IsPowered(uid))
            return true;

        return HasBatteryCharge(uid);
    }
    private void UpdatePowerVisuals(EntityUid uid)
    {
        var powered = IsOperational(uid);
        _appearance.SetData(uid, PowerDeviceVisuals.Powered, powered);
        if (_lights.TryGetLight(uid, out var light))
            _lights.SetEnabled(uid, powered, light);
    }

    private bool HasBatteryCharge(EntityUid uid)
    {
        return _powerCell.TryGetBatteryFromSlot(uid, out var battery) && battery.CurrentCharge > 0f;
    }

    private bool TryDrawHoldPower(Entity<AnomalySynchronizerComponent> ent, float seconds, bool compressing = false)
    {
        var rate = compressing || ent.Comp.Compressing ? ent.Comp.CompressPowerDraw : ent.Comp.HoldPowerDraw;
        var ok = _powerCell.TryUseCharge(ent, rate * seconds);
        UpdatePowerVisuals(ent);
        return ok;
    }

    private void ChargeBattery(Entity<AnomalySynchronizerComponent> ent, float frameTime)
    {
        if (!_powerCell.TryGetBatteryFromSlot(ent, out var batteryUid, out var battery))
            return;
        if (battery.CurrentCharge >= battery.MaxCharge)
            return;
        _battery.ChangeCharge(batteryUid.Value, ent.Comp.BatteryChargeRate * frameTime, battery);
        UpdatePowerVisuals(ent);
    }

    private void CopyBatteryStatus(EntityUid uid, AnomalySyncStatus status)
    {
        if (!_powerCell.TryGetBatteryFromSlot(uid, out var battery))
        {
            status.HasBattery = false;
            status.BatteryPercent = 0;
            return;
        }
        status.HasBattery = true;
        status.BatteryPercent = battery.MaxCharge <= 0f
            ? 0
            : (int)Math.Clamp(MathF.Round(battery.CurrentCharge / battery.MaxCharge * 100f), 0, 100);
        status.BatteryCharging = Transform(uid).Anchored && _power.IsPowered(uid) && battery.CurrentCharge < battery.MaxCharge;
    }
}

public sealed class AnomalySyncStatus
{
    public NetEntity? SynchronizerEntity;
    public NetEntity? AnomalyEntity;
    public bool Powered;
    public bool HasAnomaly;
    public string AnomalyName = string.Empty;
    public int SeverityPercent;
    public int StabilityPercent;
    public int HealthPercent;
    public string Phase = "none";
    public bool Compressing;
    public float CompressProgress;
    public float CompressDuration = 60f;
    public bool CanCompress;
    public string ScannerText = string.Empty;
    public TimeSpan? NextPulseTime;
    public bool HasBattery;
    public int BatteryPercent;
    public bool BatteryCharging;
}
