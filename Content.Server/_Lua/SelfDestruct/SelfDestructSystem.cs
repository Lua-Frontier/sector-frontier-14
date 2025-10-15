// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Audio;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Server._Lua.SelfDestruct;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._Lua.SelfDestruct;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using System.Text;

namespace Content.Server._Lua.SelfDestruct;

public sealed class SelfDestructSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ExplosionSystem _explosions = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _sound = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SelfDestructComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<SelfDestructComponent, SelfDestructEnterDigitMessage>(OnEnterDigit);
        SubscribeLocalEvent<SelfDestructComponent, SelfDestructClearMessage>(OnClear);
        SubscribeLocalEvent<SelfDestructComponent, SelfDestructConfirmWarningMessage>(OnConfirmWarning);
        SubscribeLocalEvent<SelfDestructComponent, SelfDestructSavePinMessage>(OnSavePin);
        SubscribeLocalEvent<SelfDestructComponent, SelfDestructArmMessage>(OnArm);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SelfDestructComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.CountingDown) continue;
            comp.Remaining -= frameTime;
            if (comp.Remaining <= 0)
            { comp.CountingDown = false; comp.Remaining = 0; Explode(uid); continue; }
            if (!comp.PlayedAlertSound && comp.Remaining <= 30f)
            {
                comp.PlayedAlertSound = true;
                var deedName = GetShuttleName(uid);
                var text = Loc.GetString("self-destruct-announcement-tick", ("ship", deedName), ("time", (int) comp.Remaining));
                _chat.DispatchGlobalAnnouncement(text, Loc.GetString("self-destruct-announcement-sender"), playSound: false, colorOverride: Color.Red);
            }
            if (!comp.PlayedFinalAlarm && comp.Remaining <= 13f) { comp.PlayedFinalAlarm = true; _sound.PlayGlobalOnStation(uid, _audio.ResolveSound(comp.AlertSound)); }
            UpdateState(uid, comp);
        }
    }

    private void OnUiOpened(Entity<SelfDestructComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (TryComp(ent, out ShuttleConsoleLockComponent? lockComp) && lockComp.Locked)
        { ent.Comp.Status = SelfDestructStatus.Locked; PushState(ent); return; }
        if (!ent.Comp.PinSet) ent.Comp.Status = SelfDestructStatus.Warning;
        else ent.Comp.Status = ent.Comp.CountingDown ? SelfDestructStatus.CountingDown : SelfDestructStatus.AwaitPin;
        if (!ent.Comp.CountingDown) _appearance.SetData(ent.Owner, SelfDestructVisuals.State, SelfDestructVisualState.Idle);
        PushState(ent);
    }

    private void OnEnterDigit(Entity<SelfDestructComponent> ent, ref SelfDestructEnterDigitMessage msg)
    {
        if (ent.Comp.Status is not (SelfDestructStatus.SetupPin or SelfDestructStatus.AwaitPin)) return;
        var key = GetOrCreateSessionKey(ent.Owner);
        if (key.Length >= ent.Comp.PinLength) return;
        key.Append((char)('0' + msg.Digit));
        SetSessionKey(ent.Owner, key);
        if (ent.Comp.PinSet)
        {
            var current = key.ToString();
            ent.Comp.Status = current.Length == ent.Comp.PinLength && current == ent.Comp.Pin ? SelfDestructStatus.ReadyToArm : SelfDestructStatus.AwaitPin;
        }
        else
        { ent.Comp.Status = SelfDestructStatus.SetupPin; }
        UpdateState(ent.Owner, ent.Comp);
    }

    private void OnClear(Entity<SelfDestructComponent> ent, ref SelfDestructClearMessage msg)
    {
        ClearSessionKey(ent.Owner);
        if (ent.Comp.PinSet) ent.Comp.Status = SelfDestructStatus.AwaitPin;
        UpdateState(ent.Owner, ent.Comp);
    }

    private void OnConfirmWarning(Entity<SelfDestructComponent> ent, ref SelfDestructConfirmWarningMessage msg)
    {
        if (ent.Comp.PinSet) return;
        ent.Comp.Status = SelfDestructStatus.SetupPin;
        PushState(ent);
    }

    private void OnSavePin(Entity<SelfDestructComponent> ent, ref SelfDestructSavePinMessage msg)
    {
        if (ent.Comp.PinSet) return;
        var key = GetOrCreateSessionKey(ent.Owner).ToString();
        if (key.Length != ent.Comp.PinLength)
        { _popup.PopupEntity(Loc.GetString("self-destruct-pin-length", ("len", ent.Comp.PinLength)), ent.Owner); return; }
        ent.Comp.Pin = key;
        ent.Comp.PinSet = true;
        ClearSessionKey(ent.Owner);
        ent.Comp.Status = SelfDestructStatus.AwaitPin;
        PushState(ent);
    }

    private void OnArm(Entity<SelfDestructComponent> ent, ref SelfDestructArmMessage msg)
    {
        if (!ent.Comp.PinSet || ent.Comp.CountingDown) return;
        var key = GetOrCreateSessionKey(ent.Owner).ToString();
        if (key != ent.Comp.Pin) { _popup.PopupEntity(Loc.GetString("self-destruct-wrong-pin"), ent.Owner); return; }
        ClearSessionKey(ent.Owner);
        ent.Comp.CountingDown = true;
        ent.Comp.Remaining = ent.Comp.TimerSeconds;
        ent.Comp.Status = SelfDestructStatus.CountingDown;
        Announce(ent.Owner);
        _pointLight.SetEnabled(ent.Owner, true);
        _appearance.SetData(ent.Owner, SelfDestructVisuals.State, SelfDestructVisualState.Armed);
        _sound.PlayGlobalOnStation(ent.Owner, _audio.ResolveSound(ent.Comp.ArmSound));
        var loopParams = AudioParams.Default.WithVolume(-2f).WithLoop(true).WithMaxDistance(ent.Comp.LocalLoopRange);
        _audio.PlayPvs(_audio.ResolveSound(ent.Comp.LocalLoopAlarm), ent.Owner, loopParams);
        PushState(ent);
    }

    private void Announce(EntityUid uid)
    {
        var name = Loc.GetString("self-destruct-announcement-sender");
        var deedName = GetShuttleName(uid);
        var time = 300;
        if (TryComp(uid, out SelfDestructComponent? comp)) time = comp.TimerSeconds;
        var text = Loc.GetString("self-destruct-announcement-armed", ("ship", deedName), ("time", time));
        _chat.DispatchGlobalAnnouncement(text, name, playSound: true, announcementSound: null, colorOverride: Color.Red);
    }

    private string GetShuttleName(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid != null)
        {
            var query = EntityQueryEnumerator<ShuttleDeedComponent>();
            while (query.MoveNext(out var deedUid, out var deed))
            { if (deed.ShuttleUid == xform.GridUid) return deed.ShuttleName ?? "Unknown"; }
        }
        return Name(uid);
    }

    private void UpdateState(EntityUid uid, SelfDestructComponent? comp = null)
    { if (!Resolve(uid, ref comp)) return; PushState((uid, comp)); }

    private void PushState(Entity<SelfDestructComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, SelfDestructUiKey.Key)) return;
        var key = GetOrCreateSessionKey(ent.Owner);
        var state = new SelfDestructUiState
        {
            Status = ent.Comp.Status,
            EnteredLength = key.Length,
            MaxLength = ent.Comp.PinLength,
            AllowArm = ent.Comp.Status == SelfDestructStatus.ReadyToArm || ent.Comp.Status == SelfDestructStatus.CountingDown,
            RemainingTime = (int) ent.Comp.Remaining,
            PinSet = ent.Comp.PinSet,
            EnteredText = key.ToString()
        }; _ui.SetUiState(ent.Owner, SelfDestructUiKey.Key, state);
    }

    private readonly Dictionary<EntityUid, StringBuilder> _buffers = new();
    private StringBuilder GetOrCreateSessionKey(EntityUid uid)
    {
        if (!_buffers.TryGetValue(uid, out var sb))
        { sb = new StringBuilder(); _buffers[uid] = sb; }
        return sb;
    }

    private void SetSessionKey(EntityUid uid, StringBuilder sb)
    { _buffers[uid] = sb; }

    private void ClearSessionKey(EntityUid uid)
    { if (_buffers.TryGetValue(uid, out var sb)) sb.Clear(); }

    private void Explode(EntityUid uid)
    {
        if (!TryComp(uid, out SelfDestructComponent? comp))
        { QueueDel(uid); return; }
        _explosions.QueueExplosion(uid, comp.ExplosionType, comp.TotalIntensity, comp.IntensitySlope, comp.MaxIntensity);
        _pointLight.SetEnabled(uid, false);
        _appearance.SetData(uid, SelfDestructVisuals.State, SelfDestructVisualState.Idle);
        Del(uid);
    }
}


