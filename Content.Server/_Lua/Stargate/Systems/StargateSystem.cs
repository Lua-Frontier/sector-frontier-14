// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Server._Lua.Stargate.Events;
using Content.Server.Ghost;
using Content.Server.Light.Components;
using Content.Server.Teleportation;
using Content.Shared._Lua.Stargate;
using Content.Shared._Lua.Stargate.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Lua.CLVar;
using Content.Shared.Popups;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using System.Collections.Generic;
using System.Linq;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LinkedEntitySystem _linkedEntity = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StargateAddressRegistrySystem _registry = default!;
    [Dependency] private readonly StargatePlanetGeneratorSystem _generator = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly StargateWorldPersistenceSystem _persistence = default!;

    private const float GateLightFlickerRadius = 8f;
    private static readonly Color GatePortalLightColor = Color.FromHex("#88aaff");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StargateComponent, ComponentStartup>(OnGateStartup);
        SubscribeLocalEvent<StargateComponent, MapInitEvent>(OnGateMapInit);
        SubscribeLocalEvent<StargateConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<StargateConsoleComponent, StargateDialMessage>(OnDial);
        SubscribeLocalEvent<StargateConsoleComponent, StargateInputSymbolMessage>(OnInputSymbol);
        SubscribeLocalEvent<StargateConsoleComponent, StargateClearInputMessage>(OnClearInput);
        SubscribeLocalEvent<StargateConsoleComponent, StargateClosePortalMessage>(OnClosePortal);
        SubscribeLocalEvent<StargateConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<StargateConsoleComponent, StargateSaveDiskAddressMessage>(OnSaveDiskAddress);
        SubscribeLocalEvent<StargateConsoleComponent, StargateDeleteDiskAddressMessage>(OnDeleteDiskAddress);
        SubscribeLocalEvent<StargateConsoleComponent, EntInsertedIntoContainerMessage>(OnDiskInserted);
        SubscribeLocalEvent<StargateConsoleComponent, EntRemovedFromContainerMessage>(OnDiskRemoved);
        SubscribeLocalEvent<StargateConsoleComponent, StargateToggleIrisMessage>(OnToggleIris);
        SubscribeLocalEvent<StargateConsoleComponent, StargateAutoDialFromDiskMessage>(OnAutoDialFromDisk);
        SubscribeLocalEvent<StargateComponent, EntityTeleportedThroughPortalEvent>(OnEntityPassedThroughStargate);
    }

    private const float AutoDialInterval = 0.5f;
    private readonly List<EntityUid> _autoDialBuffer = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toProcess = _autoDialBuffer;
        toProcess.Clear();
        var query = AllEntityQuery<StargateConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.AutoDialQueue == null)
                continue;
            toProcess.Add(uid);
        }

        foreach (var uid in toProcess)
        {
            if (!TryComp<StargateConsoleComponent>(uid, out var comp))
                continue;
            if (comp.AutoDialQueue == null)
                continue;

            comp.AutoDialAccumulator += frameTime;
            if (comp.AutoDialAccumulator < AutoDialInterval)
                continue;

            comp.AutoDialAccumulator -= AutoDialInterval;

            if (comp.LinkedStargate == null || !TryComp<StargateComponent>(comp.LinkedStargate, out var gate))
            {
                comp.AutoDialQueue = null;
                comp.AutoDialIndex = 0;
                UpdateUi(uid, comp);
                continue;
            }

            if (HasComp<StargateDialingComponent>(comp.LinkedStargate.Value) || IsGateBusy(comp.LinkedStargate.Value))
            {
                comp.AutoDialQueue = null;
                comp.AutoDialIndex = 0;
                UpdateUi(uid, comp);
                continue;
            }

            if (TryComp<StargateControllableComponent>(comp.LinkedStargate.Value, out var srcCtrl) && !srcCtrl.Enabled)
            {
                _popup.PopupEntity(Loc.GetString("stargate-console-iris-closed"), uid);
                _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
                comp.AutoDialQueue = null;
                comp.AutoDialIndex = 0;
                UpdateUi(uid, comp);
                continue;
            }

            if (comp.AutoDialIndex < comp.AutoDialQueue.Length)
            {
                var symbol = comp.AutoDialQueue[comp.AutoDialIndex];
                var requiredLen = GetRequiredLength(comp.CurrentInput, symbol);
                if (comp.CurrentInput.Count >= requiredLen || symbol < 1 || symbol > gate.SymbolCount || comp.CurrentInput.Contains(symbol))
                {
                    comp.AutoDialQueue = null;
                    comp.AutoDialIndex = 0;
                    UpdateUi(uid, comp);
                    continue;
                }

                comp.CurrentInput.Add(symbol);
                comp.AutoDialIndex++;
                _audio.PlayPvs(comp.DhdPressSound, uid, GateDhdPressParams);
                UpdateUi(uid, comp);
                continue;
            }

            var symbols = comp.AutoDialQueue.ToArray();
            comp.AutoDialQueue = null;
            comp.AutoDialIndex = 0;
            comp.CurrentInput.Clear();

            var requiredLenFull = GetRequiredLength(symbols);
            if (!ValidateDialAddress(symbols, requiredLenFull, gate.SymbolCount))
            {
                _popup.PopupEntity(Loc.GetString("stargate-console-invalid-address"), uid);
                _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
                UpdateUi(uid, comp);
                continue;
            }

            ClosePortal(comp.LinkedStargate.Value);

            if (gate.Address != null && gate.Address.Length == symbols.Length && gate.Address.AsSpan().SequenceEqual(symbols))
            {
                _popup.PopupEntity(Loc.GetString("stargate-console-dial-failed"), uid);
                _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
                UpdateUi(uid, comp);
                continue;
            }

            _audio.PlayPvs(comp.DhdDialSound, uid, GateSoundParams);
            if (TryDialExistingGate(uid, comp, gate, symbols))
            {
                UpdateUi(uid, comp);
                continue;
            }

            if (requiredLenFull == 7)
            {
                _popup.PopupEntity(Loc.GetString("stargate-console-dial-failed"), uid);
                _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
                UpdateUi(uid, comp);
                continue;
            }

            if (!_registry.IsPoolAddress(symbols))
            {
                _popup.PopupEntity(Loc.GetString("stargate-console-dial-failed"), uid);
                _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
                UpdateUi(uid, comp);
                continue;
            }

            var seed = _registry.ComputeSeed(symbols);

            if (!_registry.TryGetDestination(symbols, out var destMapUid, out _))
                EnsureDestinationCreated(symbols, seed, ref destMapUid);

            if (!TryComp<StargateDestinationComponent>(destMapUid, out var destComp) || destComp.GateUid == null)
            {
                _popup.PopupEntity(Loc.GetString("stargate-console-dial-failed"), uid);
                _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
                UpdateUi(uid, comp);
                continue;
            }

            StartDialing(comp.LinkedStargate.Value, gate, destComp.GateUid.Value, destMapUid, symbols, uid);
            UpdateUi(uid, comp);
        }
    }

    private void OnAutoDialFromDisk(EntityUid uid, StargateConsoleComponent comp, StargateAutoDialFromDiskMessage args)
    {
        if (comp.LinkedStargate == null || !TryComp<StargateComponent>(comp.LinkedStargate, out var gate))
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-no-gate"), uid);
            return;
        }

        if (HasComp<StargateDialingComponent>(comp.LinkedStargate.Value))
            return;

        if (IsGateBusy(comp.LinkedStargate.Value))
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-gate-busy"), uid);
            return;
        }

        var address = args.Address;
        var requiredLen = GetRequiredLength(address);

        if (!ValidateDialAddress(address, requiredLen, gate.SymbolCount))
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-invalid-address"), uid);
            _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
            return;
        }

        comp.CurrentInput.Clear();
        comp.AutoDialQueue = address.ToArray();
        comp.AutoDialIndex = 0;
        comp.AutoDialAccumulator = AutoDialInterval;
        UpdateUi(uid, comp);
    }
    public void UpdateGateVisualState(EntityUid gate, StargateVisualState state)
    {
        _appearance.SetData(gate, StargateVisuals.State, state);
        if (TryComp(gate, out PointLightComponent? pl))
        {
            var lightOn = state is StargateVisualState.Opening or StargateVisualState.Idle or StargateVisualState.Closing;
            _pointLight.SetEnabled(gate, lightOn, pl);
            if (lightOn)
            {
                _pointLight.SetColor(gate, GatePortalLightColor, pl);
                _pointLight.SetRadius(gate, 4f, pl);
                _pointLight.SetEnergy(gate, 0.6f, pl);
            }
        }
        if (state == StargateVisualState.Opening) FlickerNearbyLights(gate);
    }

    private void FlickerNearbyLights(EntityUid gate)
    {
        var lights = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(gate, GateLightFlickerRadius, lights, LookupFlags.StaticSundries);
        var query = GetEntityQuery<PoweredLightComponent>();
        foreach (var uid in lights)
        {
            if (uid == gate || !query.HasComponent(uid)) continue;
            _ghost.DoGhostBooEvent(uid);
        }
    }
    private void OnGateStartup(EntityUid uid, StargateComponent comp, ComponentStartup args)
    {
        UpdateGateVisualState(uid, StargateVisualState.Off);

        if (TryComp<StargateControllableComponent>(uid, out var ctrl))
        {
            var irisState = ctrl.Enabled
                ? StargateIrisVisualState.Open
                : StargateIrisVisualState.Closed;
            _appearance.SetData(uid, StargateVisuals.IrisState, irisState);
        }
    }

    private void OnGateMapInit(EntityUid uid, StargateComponent comp, MapInitEvent args)
    {
        if (!string.IsNullOrEmpty(comp.AddressPreset))
        {
            var preset = comp.AddressPreset.Trim();
            var parsed = StargateGlyphs.ParseAddress(preset);

            if (parsed == null)
            { Log.Error($"Stargate {ToPrettyString(uid)}: failed to parse addressPreset '{preset}' — contains invalid glyph characters (valid: A-Z, a-n)."); }
            else if (parsed.Length != comp.AddressLength)
            { Log.Error($"Stargate {ToPrettyString(uid)}: addressPreset '{preset}' length {parsed.Length} does not match addressLength {comp.AddressLength}."); }
            else
            { comp.Address = parsed; }
        }

        if (comp.Address == null)
        {
            if (HasComp<StargateControllableComponent>(uid))
                _registry.AssignPrivateAddress(uid, comp);
            else
                _registry.AssignAddress(uid, comp);
        }
    }

    private static readonly SoundPathSpecifier IrisSound =
        new("/Audio/_Lua/Effects/Stargate/iris_thud_2.ogg");

    private static readonly AudioParams GateSoundParams = AudioParams.Default.WithVolume(
        SharedAudioSystem.GainToVolume(0.25f));
    private static readonly AudioParams GateDhdPressParams = AudioParams.Default.WithVolume(
        SharedAudioSystem.GainToVolume(0.2f));
    private const float IrisAnimDuration = 1.344f;

    private void OnEntityPassedThroughStargate(EntityUid uid, StargateComponent comp, EntityTeleportedThroughPortalEvent args)
    {
        var curTime = _timing.CurTime;
        if (TryComp<StargatePortalTimerComponent>(args.Portal, out var timer))
        {
            timer.HasEntityPassedThrough = true;
            timer.LastEntityNearTime = curTime;
        }
        if (args.TargetEntity is { } other && TryComp<StargatePortalTimerComponent>(other, out var otherTimer))
        {
            otherTimer.HasEntityPassedThrough = true;
            otherTimer.LastEntityNearTime = curTime;
        }
    }

    private void OnToggleIris(EntityUid uid, StargateConsoleComponent comp, StargateToggleIrisMessage args)
    {
        if (comp.LinkedStargate == null)
            return;

        var gateUid = comp.LinkedStargate.Value;

        if (!TryComp<StargateControllableComponent>(gateUid, out var controllable))
            return;

        if (HasComp<StargateIrisAnimatingComponent>(gateUid))
            return;

        if (controllable.Enabled)
        {
            if (IsGateBusy(gateUid))
                ClosePortal(gateUid);

            CancelDialing(gateUid);
            controllable.Enabled = false;
            StartIrisAnimation(gateUid, false);
            _popup.PopupEntity(Loc.GetString("stargate-controllable-deactivated"), gateUid);
        }
        else
        {
            controllable.Enabled = true;
            StartIrisAnimation(gateUid, true);
            _popup.PopupEntity(Loc.GetString("stargate-controllable-activated"), gateUid);
        }

        UpdateUi(uid, comp);
    }

    public void StartIrisAnimation(EntityUid gateUid, bool opening)
    {
        _audio.PlayPvs(IrisSound, gateUid, GateSoundParams);

        var irisVisual = opening
            ? StargateIrisVisualState.Opening
            : StargateIrisVisualState.Closing;
        _appearance.SetData(gateUid, StargateVisuals.IrisState, irisVisual);

        var anim = EnsureComp<StargateIrisAnimatingComponent>(gateUid);
        anim.Accumulator = 0f;
        anim.Duration = IrisAnimDuration;
        anim.IsOpening = opening;
    }

    public void FinishIrisAnimation(EntityUid gateUid, bool wasOpening)
    {
        var finalState = wasOpening
            ? StargateIrisVisualState.Open
            : StargateIrisVisualState.Closed;
        _appearance.SetData(gateUid, StargateVisuals.IrisState, finalState);
        RemComp<StargateIrisAnimatingComponent>(gateUid);
    }

    private void OnConsoleMapInit(EntityUid uid, StargateConsoleComponent comp, MapInitEvent args)
    {
        if (comp.LinkedStargate != null)
            return;

        var xform = Transform(uid);
        var nearby = new HashSet<Entity<StargateComponent>>();
        _lookup.GetEntitiesInRange(xform.Coordinates, comp.AutoLinkRadius, nearby);

        EntityUid? closest = null;
        var closestDist = float.MaxValue;

        foreach (var ent in nearby)
        {
            var dist = (Transform(ent).WorldPosition - xform.WorldPosition).Length();
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = ent;
            }
        }

        if (closest != null)
            comp.LinkedStargate = closest;
    }

    private void OnUiOpened(EntityUid uid, StargateConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    private void OnInputSymbol(EntityUid uid, StargateConsoleComponent comp, StargateInputSymbolMessage args)
    {
        if (comp.LinkedStargate == null || !TryComp<StargateComponent>(comp.LinkedStargate, out var gate))
            return;

        if (HasComp<StargateDialingComponent>(comp.LinkedStargate.Value))
            return;

        if (IsGateBusy(comp.LinkedStargate.Value))
            return;

        var requiredLen = GetRequiredLength(comp.CurrentInput, args.Symbol);
        if (comp.CurrentInput.Count >= requiredLen)
            return;

        if (args.Symbol < 1 || args.Symbol > gate.SymbolCount)
            return;

        if (comp.CurrentInput.Contains(args.Symbol))
            return;

        comp.CurrentInput.Add(args.Symbol);

        _audio.PlayPvs(comp.DhdPressSound, uid, GateDhdPressParams);

        UpdateUi(uid, comp);
    }

    private void OnClearInput(EntityUid uid, StargateConsoleComponent comp, StargateClearInputMessage args)
    {
        comp.CurrentInput.Clear();
        UpdateUi(uid, comp);
    }

    private void OnClosePortal(EntityUid uid, StargateConsoleComponent comp, StargateClosePortalMessage args)
    {
        if (comp.LinkedStargate == null)
            return;

        if (HasComp<StargateDialingComponent>(comp.LinkedStargate.Value))
            CancelDialing(comp.LinkedStargate.Value);

        ClosePortal(comp.LinkedStargate.Value);
        UpdateUi(uid, comp);
    }

    private void OnSaveDiskAddress(EntityUid uid, StargateConsoleComponent comp, StargateSaveDiskAddressMessage args)
    {
        if (comp.LinkedStargate == null || !TryComp<StargateComponent>(comp.LinkedStargate, out var gate))
            return;

        if (gate.Address == null || gate.Address.Length == 0)
            return;

        var diskEntity = GetInsertedDisk(uid);
        if (diskEntity == null || !TryComp<StargateAddressDiskComponent>(diskEntity, out var disk))
            return;

        foreach (var existing in disk.Addresses)
        {
            if (existing.Count != gate.Address.Length)
                continue;

            var match = true;
            for (var i = 0; i < existing.Count; i++)
            {
                if (existing[i] != gate.Address[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return;
        }

        disk.Addresses.Add(new List<byte>(gate.Address));
        Dirty(diskEntity.Value, disk);
        UpdateUi(uid, comp);
    }

    private void OnDeleteDiskAddress(EntityUid uid, StargateConsoleComponent comp, StargateDeleteDiskAddressMessage args)
    {
        var diskEntity = GetInsertedDisk(uid);
        if (diskEntity == null || !TryComp<StargateAddressDiskComponent>(diskEntity, out var disk))
            return;

        if (args.Index < 0 || args.Index >= disk.Addresses.Count)
            return;

        disk.Addresses.RemoveAt(args.Index);
        Dirty(diskEntity.Value, disk);
        UpdateUi(uid, comp);
    }

    private void OnDiskInserted(EntityUid uid, StargateConsoleComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == "disk_slot")
            UpdateUi(uid, comp);
    }

    private void OnDiskRemoved(EntityUid uid, StargateConsoleComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == "disk_slot")
            UpdateUi(uid, comp);
    }

    private EntityUid? GetInsertedDisk(EntityUid consoleUid)
    {
        if (!_itemSlots.TryGetSlot(consoleUid, "disk_slot", out var slot))
            return null;

        return slot.Item;
    }

    private void OnDial(EntityUid uid, StargateConsoleComponent comp, StargateDialMessage args)
    {
        if (comp.LinkedStargate == null || !TryComp<StargateComponent>(comp.LinkedStargate, out var gate))
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-no-gate"), uid);
            return;
        }

        if (!_cfg.GetCVar(CLVars.StargateEnabled))
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-overloaded"), uid);
            _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
            return;
        }

        if (HasComp<StargateDialingComponent>(comp.LinkedStargate.Value))
            return;

        if (IsGateBusy(comp.LinkedStargate.Value))
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-gate-busy"), uid);
            return;
        }

        if (TryComp<StargateControllableComponent>(comp.LinkedStargate, out var srcCtrl) && !srcCtrl.Enabled)
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-iris-closed"), uid);
            _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
            return;
        }

        var symbols = args.Symbols;
        var requiredLen = GetRequiredLength(symbols);

        if (!ValidateDialAddress(symbols, requiredLen, gate.SymbolCount))
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-invalid-address"), uid);
            _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
            return;
        }

        ClosePortal(comp.LinkedStargate.Value);

        if (gate.Address != null && gate.Address.Length == symbols.Length && gate.Address.SequenceEqual(symbols))
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-dial-failed"), uid);
            _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
            return;
        }

        _audio.PlayPvs(comp.DhdDialSound, uid, GateSoundParams);
        if (TryDialExistingGate(uid, comp, gate, symbols))
            return;
        if (requiredLen == 7)
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-dial-failed"), uid);
            _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
            return;
        }
        if (!_registry.IsPoolAddress(symbols))
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-dial-failed"), uid);
            _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
            return;
        }
        var seed = _registry.ComputeSeed(symbols);

        if (!_registry.TryGetDestination(symbols, out var destMapUid, out _))
            EnsureDestinationCreated(symbols, seed, ref destMapUid);

        if (!TryComp<StargateDestinationComponent>(destMapUid, out var destComp) || destComp.GateUid == null)
        {
            _popup.PopupEntity(Loc.GetString("stargate-console-dial-failed"), uid);
            _audio.PlayPvs(gate.DialFailSound, comp.LinkedStargate.Value, GateSoundParams);
            return;
        }

        StartDialing(comp.LinkedStargate.Value, gate, destComp.GateUid.Value, destMapUid, symbols, uid);

        comp.CurrentInput.Clear();
        UpdateUi(uid, comp);
    }
    private bool TryDialExistingGate(EntityUid consoleUid, StargateConsoleComponent comp, StargateComponent gate, byte[] symbols)
    {
        var query = AllEntityQuery<StargateComponent>();
        while (query.MoveNext(out var gateUid, out var otherGate))
        {
            if (gateUid == comp.LinkedStargate)
                continue;

            if (otherGate.Address == null || otherGate.Address.Length != symbols.Length)
                continue;

            var match = true;
            for (var i = 0; i < symbols.Length; i++)
            {
                if (otherGate.Address[i] != symbols[i])
                {
                    match = false;
                    break;
                }
            }

            if (!match)
                continue;

            if (TryComp<StargateControllableComponent>(gateUid, out var controllable) && !controllable.Enabled)
                continue;

            var mapUid = Transform(gateUid).MapUid;
            if (mapUid == null)
                continue;

            StartDialing(comp.LinkedStargate!.Value, gate, gateUid, mapUid.Value, symbols, consoleUid);
            comp.CurrentInput.Clear();
            UpdateUi(consoleUid, comp);
            return true;
        }

        return false;
    }
    private void StartDialing(EntityUid sourceGate, StargateComponent sourceComp,
        EntityUid destGate, EntityUid destMapUid, byte[] symbols, EntityUid consoleUid)
    {
        var dialing = EnsureComp<StargateDialingComponent>(sourceGate);
        dialing.Symbols = symbols;
        dialing.DestGateUid = destGate;
        dialing.DestMapUid = destMapUid;
        dialing.ChevronIndex = 0;
        dialing.Accumulator = 0f;
        dialing.InKawoosh = false;
        dialing.ChevronDelay = sourceComp.ChevronDelay;
        dialing.KawooshDelay = sourceComp.KawooshDelay;
        dialing.ConsoleUid = consoleUid;

        RemComp<StargateClosingComponent>(sourceGate);
        UpdateGateVisualState(sourceGate, StargateVisualState.Starting);
    }

    public void CancelDialing(EntityUid gateUid)
    {
        RemComp<StargateDialingComponent>(gateUid);
        UpdateGateVisualState(gateUid, StargateVisualState.Off);
    }
    public void FinishDialing(EntityUid sourceGate, StargateDialingComponent dialing, StargateComponent gate)
    {
        OpenPortal(sourceGate, gate, dialing.DestGateUid, dialing.DestMapUid);
        RemComp<StargateDialingComponent>(sourceGate);

        if (TryComp<StargateConsoleComponent>(dialing.ConsoleUid, out var console))
            UpdateUi(dialing.ConsoleUid, console);
    }

    private void OpenPortal(EntityUid sourceGate, StargateComponent sourceComp, EntityUid destGate, EntityUid destMapUid)
    {
        if (!destGate.IsValid() || !Exists(destGate) || !destMapUid.IsValid() || !Exists(destMapUid))
        {
            Log.Warning("OpenPortal skipped: destination gate or map no longer valid (e.g. map was unloaded). Source: {Source}, destGate: {DestGate}, destMap: {DestMap}",
                ToPrettyString(sourceGate), destGate, destMapUid);
            return;
        }

        var ev = new AttemptStargateOpenEvent(destMapUid, destGate);
        RaiseLocalEvent(destMapUid, ref ev);

        if (ev.Cancelled)
            return;
        ClosePortal(sourceGate);
        ClosePortal(destGate);

        _linkedEntity.OneWayLink(sourceGate, destGate);

        RemComp<StargateClosingComponent>(sourceGate);
        RemComp<StargateClosingComponent>(destGate);
        UpdateGateVisualState(sourceGate, StargateVisualState.Idle);

        if (TryComp<StargateComponent>(destGate, out var destComp))
        {
            _audio.PlayPvs(destComp.OpenSound, destGate, GateSoundParams);
            UpdateGateVisualState(destGate, StargateVisualState.Opening);
            var opening = EnsureComp<StargateOpeningComponent>(destGate);
            opening.Accumulator = 0f;
            opening.Duration = destComp.KawooshDelay;
        }
        else
        {
            UpdateGateVisualState(destGate, StargateVisualState.Idle);
        }

        var idleParams = AudioParams.Default.WithLoop(true).WithVolume(SharedAudioSystem.GainToVolume(0.35f)).WithMaxDistance(10f);
        sourceComp.IdleSoundEntity = _audio.PlayPvs(sourceComp.IdleSound, sourceGate, idleParams)?.Entity;

        if (destComp != null)
            destComp.IdleSoundEntity = _audio.PlayPvs(destComp.IdleSound, destGate, idleParams)?.Entity;

        var timer = EnsureComp<StargatePortalTimerComponent>(sourceGate);
        timer.HasEntityPassedThrough = false;
        timer.LastEntityNearTime = default;

        var destTimer = EnsureComp<StargatePortalTimerComponent>(destGate);
        destTimer.HasEntityPassedThrough = false;
        destTimer.LastEntityNearTime = default;

        var openEv = new StargateOpenEvent(destMapUid, sourceGate, destGate);
        RaiseLocalEvent(destMapUid, ref openEv);
    }

    public void ClosePortal(EntityUid gateUid, StargateComponent? comp = null)
    {
        if (!Resolve(gateUid, ref comp, false))
            return;

        if (_linkedEntity.GetLink(gateUid, out var dest))
        {
            comp.IdleSoundEntity = _audio.Stop(comp.IdleSoundEntity);

            _audio.PlayPvs(comp.CloseSound, gateUid, GateSoundParams);
            if (TryComp<StargateComponent>(dest, out var destComp))
            {
                destComp.IdleSoundEntity = _audio.Stop(destComp.IdleSoundEntity);
                _audio.PlayPvs(destComp.CloseSound, dest.Value, GateSoundParams);
            }

            RemComp<LinkedEntityComponent>(gateUid);
            RemComp<StargatePortalTimerComponent>(gateUid);

            StartClosingAnimation(gateUid);
            StartClosingAnimation(dest.Value);
            return;
        }
        var sourceGate = FindSourceGate(gateUid);
        if (sourceGate != null)
        {
            ClosePortal(sourceGate.Value);
            return;
        }
        RemComp<StargatePortalTimerComponent>(gateUid);
        UpdateGateVisualState(gateUid, StargateVisualState.Off);
    }
    private void StartClosingAnimation(EntityUid gateUid)
    {
        UpdateGateVisualState(gateUid, StargateVisualState.Closing);
        var closing = EnsureComp<StargateClosingComponent>(gateUid);
        closing.Accumulator = 0f;
    }

    private EntityUid? FindSourceGate(EntityUid destGateUid)
    {
        var query = AllEntityQuery<LinkedEntityComponent, StargateComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (_linkedEntity.GetLink(uid, out var linked) && linked == destGateUid)
                return uid;
        }
        return null;
    }
    private bool IsGateBusy(EntityUid gateUid)
    {
        if (_linkedEntity.GetLink(gateUid, out _))
            return true;

        return FindSourceGate(gateUid) != null;
    }
    private static int GetRequiredLength(List<byte> currentInput, byte nextSymbol)
    {
        if (currentInput.Count == 0 && nextSymbol == 1)
            return 7;
        if (currentInput.Count > 0 && currentInput[0] == 1)
            return 7;
        return 6;
    }

    private static int GetRequiredLength(byte[] symbols)
    {
        if (symbols.Length > 0 && symbols[0] == 1)
            return 7;
        return 6;
    }

    private static bool ValidateDialAddress(byte[] symbols, int requiredLen, byte symbolCount)
    {
        if (symbols.Length != requiredLen)
            return false;

        var seen = new HashSet<byte>();
        foreach (var s in symbols)
        {
            if (s < 1 || s > symbolCount)
                return false;
            if (!seen.Add(s))
                return false;
        }

        return true;
    }

    public void UpdateUi(EntityUid consoleUid, StargateConsoleComponent comp)
    {
        var portalOpen = false;
        byte maxSymbols = 40;
        byte[] gateAddress = Array.Empty<byte>();
        var dialing = false;

        if (comp.LinkedStargate != null && TryComp<StargateComponent>(comp.LinkedStargate, out var gate))
        {
            portalOpen = IsGateBusy(comp.LinkedStargate.Value);
            maxSymbols = gate.SymbolCount;
            gateAddress = gate.Address ?? Array.Empty<byte>();
            dialing = HasComp<StargateDialingComponent>(comp.LinkedStargate.Value);
        }

        byte[][]? diskAddresses = null;
        var diskEntity = GetInsertedDisk(consoleUid);
        if (diskEntity != null && TryComp<StargateAddressDiskComponent>(diskEntity, out var disk))
        {
            diskAddresses = new byte[disk.Addresses.Count][];
            for (var i = 0; i < disk.Addresses.Count; i++)
                diskAddresses[i] = disk.Addresses[i].ToArray();
        }

        StargateControllableComponent? ctrl = null;
        var hasControllable = comp.LinkedStargate != null
            && TryComp(comp.LinkedStargate, out ctrl);
        var irisOpen = hasControllable && ctrl!.Enabled;

        var autoDialing = comp.AutoDialQueue != null;

        var state = new StargateConsoleUiState(
            comp.CurrentInput.ToArray(),
            portalOpen,
            maxSymbols,
            gateAddress,
            dialing,
            autoDialing,
            diskAddresses,
            hasControllable,
            irisOpen
        );

        _ui.SetUiState(consoleUid, StargateConsoleUiKey.Key, state);
    }

    public void EnsureDestinationCreated(byte[] symbols, int seed, ref EntityUid destMapUid)
    {
        var loadedFromSave = false;
        if (_cfg.GetCVar(CLVars.StargateWorldSavesEnabled))
        {
            var key = StargateAddressRegistrySystem.AddressToKey(symbols);
            var path = StargateWorldPersistenceSystem.GetSavePath(key);
            if (_persistence.SaveExists(key))
            {
                Log.Info("Loading StarGate world from save: key={Key}, path={Path}", key, path);
                if (_persistence.TryLoadStargateWorld(path, out var loadResult))
                {
                    if (loadResult.Maps.Count == 1)
                    {
                        var mapEntity = loadResult.Maps.First();
                        destMapUid = mapEntity.Owner;
                        if (TryComp<StargateDestinationComponent>(destMapUid, out var destCompLoaded))
                        {
                            var gateOnMap = FindGateOnMap(destMapUid);
                            if (gateOnMap != null)
                            {
                                destCompLoaded.GateUid = gateOnMap;
                                destCompLoaded.Loaded = true;
                                var consoleOnMap = FindConsoleLinkedToGate(gateOnMap.Value);
                                if (consoleOnMap != null && TryComp<StargateConsoleComponent>(consoleOnMap, out var consoleComp)) consoleComp.LinkedStargate = gateOnMap;
                                _registry.RegisterDestination(symbols, destMapUid, destCompLoaded.Seed);
                                loadedFromSave = true;
                                Log.Info("StarGate world loaded successfully from save: key={Key}, map={Map}", key, ToPrettyString(destMapUid));
                            }
                        }
                    }
                    if (!loadedFromSave)
                    {
                        Log.Error("Failed to restore StarGate world from save (missing gate or dest comp): key={Key}", key);
                        _mapLoader.Delete(loadResult);
                    }
                }
                else
                {
                    Log.Error("Failed to load StarGate world from save file: key={Key}, path={Path}", key, path);
                }
            }
        }
        if (!loadedFromSave)
        {
            var result = _generator.CreateDestinationMap(symbols, seed);
            destMapUid = result.MapUid;
            _registry.RegisterDestination(symbols, destMapUid, seed);
        }
    }

    private EntityUid? FindGateOnMap(EntityUid mapUid)
    {
        var query = AllEntityQuery<StargateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        { if (xform.MapUid == mapUid) return uid; }
        return null;
    }

    private EntityUid? FindConsoleLinkedToGate(EntityUid gateUid)
    {
        var mapUid = Transform(gateUid).MapUid;
        if (mapUid == null) return null;
        var query = AllEntityQuery<StargateConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        { if (xform.MapUid == mapUid) return uid; }
        return null;
    }
}
