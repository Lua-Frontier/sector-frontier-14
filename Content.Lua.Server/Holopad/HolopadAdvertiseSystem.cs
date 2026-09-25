// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared.Chat.Systems;
using Content.Lua.Shared.Holopad;
using Content.Server.Chat.Systems;
using Content.Server.Clothing.Systems;
using Content.Server.Holopad;
using Content.Shared.Access.Systems;
using Content.Shared.Corvax.TTS;
using Content.Shared.Holopad;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Interaction;
using Content.Shared.Preferences;
using Content.Shared.Telephone;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Server.GameObjects;

namespace Content.Lua.Server.Holopad;

public sealed class HolopadAdvertiseSystem : SharedHolopadSystem, IHolopadAdvertiseSystem
{
    [Dependency] private readonly HolopadSystem _holopad = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoidSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private sealed class ScriptedBroadcastState
    {
        public int CurrentIndex;
        public TimeSpan NextStepTime;
        public EntityUid? Actor;
        public EntityUid? Avatar;
        public bool Completed;
        public readonly HashSet<EntityUid> LinkedHolopads = new();
    }

    private readonly Dictionary<EntityUid, ScriptedBroadcastState> _scriptedBroadcasts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HolopadAdvertiseComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(EntityUid uid, HolopadAdvertiseComponent advertise, InteractHandEvent args)
    {
        if (!TryComp<HolopadComponent>(uid, out var holopad))
            return;

        if (advertise.ScriptedMessages.Count == 0)
            return;

        var entity = new Entity<HolopadComponent>(uid, holopad);
        if (IsHolopadControlLocked(entity, args.User) || IsHolopadBroadcastOnCoolDown(entity))
            return;

        if (!_accessReaderSystem.IsAllowed(args.User, uid))
            return;

        StartScriptedBroadcast(uid, holopad, advertise, args.User);
        args.Handled = true;
    }

    public bool TryStartScriptedBroadcast(EntityUid holopadUid, EntityUid? actor)
    {
        if (!TryComp(holopadUid, out HolopadComponent? holopad)
            || !TryComp(holopadUid, out HolopadAdvertiseComponent? advertise)
            || advertise.ScriptedMessages.Count == 0)
            return false;

        StartScriptedBroadcast(holopadUid, holopad, advertise, actor);
        return true;
    }

    public void UpdateScriptedBroadcasts()
    {
        if (_scriptedBroadcasts.Count == 0)
            return;

        var now = _timing.CurTime;
        var finished = new List<EntityUid>();
        foreach (var (uid, state) in _scriptedBroadcasts)
        {
            if (!TryComp(uid, out HolopadComponent? holopad)
                || !TryComp(uid, out HolopadAdvertiseComponent? advertise)
                || advertise.ScriptedMessages.Count == 0)
            {
                finished.Add(uid);
                continue;
            }

            if (now < state.NextStepTime)
                continue;

            if (state.CurrentIndex >= advertise.ScriptedMessages.Count)
            {
                if (!state.Completed)
                {
                    state.Completed = true;
                    state.NextStepTime = now + TimeSpan.FromSeconds(advertise.ScriptedEndDelaySeconds);
                    _appearanceSystem.SetData(uid, TelephoneVisuals.Key, TelephoneState.EndingCall);
                }
                else
                {
                    finished.Add(uid);
                }

                continue;
            }

            if (state.CurrentIndex < 0)
            {
                finished.Add(uid);
                continue;
            }

            var step = advertise.ScriptedMessages[state.CurrentIndex];
            RunScriptedMessage(uid, holopad, advertise, state.Actor, state.Avatar, step);
            state.CurrentIndex++;
            if (state.CurrentIndex < advertise.ScriptedMessages.Count)
            {
                var next = advertise.ScriptedMessages[state.CurrentIndex];
                state.NextStepTime = now + TimeSpan.FromSeconds(next.DelaySeconds);
            }
        }

        foreach (var uid in finished)
        {
            if (_scriptedBroadcasts.TryGetValue(uid, out var state))
            {
                if (state.Avatar != null && Exists(state.Avatar.Value))
                    QueueDel(state.Avatar.Value);

                foreach (var linkedUid in state.LinkedHolopads)
                {
                    if (!Exists(linkedUid))
                        continue;

                    if (!TryComp<HolopadComponent>(linkedUid, out var linkedComp))
                        continue;

                    var linkedEnt = new Entity<HolopadComponent>(linkedUid, linkedComp);
                    if (linkedComp.Hologram != null)
                        _holopad.DeleteHologram(linkedComp.Hologram.Value, linkedEnt);

                    _holopad.SetHolopadAmbientState(linkedEnt, false);
                }
            }

            _scriptedBroadcasts.Remove(uid);
            if (TryComp<HolopadComponent>(uid, out var holopadEnd))
            {
                var ent = new Entity<HolopadComponent>(uid, holopadEnd);
                if (holopadEnd.Hologram != null)
                    _holopad.DeleteHologram(holopadEnd.Hologram.Value, ent);

                _holopad.SetHolopadAmbientState(ent, false);
                _appearanceSystem.SetData(uid, TelephoneVisuals.Key, TelephoneState.Idle);
            }
        }
    }

    private void StartScriptedBroadcast(
        EntityUid uid,
        HolopadComponent holopad,
        HolopadAdvertiseComponent advertise,
        EntityUid? actor)
    {
        if (advertise.ScriptedMessages.Count == 0)
            return;

        var source = new Entity<HolopadComponent>(uid, holopad);
        var state = new ScriptedBroadcastState
        {
            Actor = actor,
            CurrentIndex = 0,
        };
        var first = advertise.ScriptedMessages[0];
        state.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(advertise.ScriptedStartDelaySeconds + first.DelaySeconds);
        state.Avatar = EnsureScriptedAvatar(uid, advertise);
        _scriptedBroadcasts[uid] = state;
        _holopad.SetHolopadAmbientState(source, true);
        holopad.ControlLockoutOwner = actor;
        holopad.ControlLockoutStartTime = _timing.CurTime;
        Dirty(uid, holopad);
        _appearanceSystem.SetData(uid, TelephoneVisuals.Key, TelephoneState.Ringing);
    }

    private EntityUid? EnsureScriptedAvatar(EntityUid holopadUid, HolopadAdvertiseComponent advertise)
    {
        if (advertise.ScriptedAvatarProtoId == null)
            return null;

        var container = _container.EnsureContainer<Container>(holopadUid, "holopad-scripted-avatar");
        EntityUid? existing = null;
        foreach (var ent in container.ContainedEntities)
        {
            existing = ent;
            break;
        }

        if (existing != null && Exists(existing.Value))
            return existing.Value;

        var coords = Transform(holopadUid).Coordinates;
        var avatar = Spawn(advertise.ScriptedAvatarProtoId, coords);
        ConfigureScriptedAvatarAppearance(avatar, advertise);
        if (!string.IsNullOrEmpty(advertise.ScriptedAvatarOutfitId))
        {
            var outfit = EntitySystem.Get<OutfitSystem>();
            outfit.SetOutfit(avatar, advertise.ScriptedAvatarOutfitId);
        }

        _container.Insert(avatar, container);
        return avatar;
    }

    private void ConfigureScriptedAvatarAppearance(EntityUid avatar, HolopadAdvertiseComponent advertise)
    {
        if (advertise.ScriptedAvatarAppearance is not { } settings)
            return;

        var profile = HumanoidCharacterProfile.DefaultWithSpecies(settings.Species)
            .WithName(settings.Name)
            .WithAge(settings.Age)
            .WithSex(settings.Sex)
            .WithGender(settings.Gender)
            .WithVoice(settings.Voice);
        var markings = new List<Marking>();
        foreach (var marking in settings.Markings)
            markings.Add(new Marking(marking.MarkingId, marking.MarkingColors));

        var appearance = new HumanoidCharacterAppearance(
            hairStyleId: settings.HairStyleId,
            hairColor: settings.HairColor,
            facialHairStyleId: settings.FacialHairStyleId,
            facialHairColor: settings.FacialHairColor,
            eyeColor: settings.EyeColor,
            skinColor: settings.SkinColor,
            markings: markings);
        appearance.HairGradientEnabled = false;
        appearance.FacialHairGradientEnabled = false;
        appearance.AllMarkingsGradientEnabled = false;
        profile = profile.WithCharacterAppearance(appearance);
        _humanoidSystem.LoadProfile(avatar, profile);
    }

    private void RunScriptedMessage(
        EntityUid uid,
        HolopadComponent holopad,
        HolopadAdvertiseComponent advertise,
        EntityUid? actor,
        EntityUid? avatar,
        HolopadScriptedMessageStep step)
    {
        if (string.IsNullOrWhiteSpace(step.Message))
            return;

        var senderName = advertise.ScriptedSenderName ?? MetaData(uid).EntityName;
        var voiceId = step.VoiceId ?? advertise.ScriptedVoiceId;
        PlayScriptedMessageOnHolopad(uid, holopad, advertise, avatar, step, senderName, voiceId);
        if (!advertise.ScriptedBroadcastToSector)
            return;

        var query = AllEntityQuery<HolopadAdvertiseComponent, HolopadComponent>();
        while (query.MoveNext(out var otherUid, out var otherAdvertise, out var otherHolopad))
        {
            if (otherUid == uid)
                continue;

            if (_scriptedBroadcasts.TryGetValue(uid, out var state))
                state.LinkedHolopads.Add(otherUid);

            EntityUid? otherAvatar = null;
            if (advertise.ScriptedAvatarProtoId != null)
            {
                if (otherAdvertise.ScriptedAvatarProtoId == null)
                    otherAdvertise.ScriptedAvatarProtoId = advertise.ScriptedAvatarProtoId;
                if (otherAdvertise.ScriptedAvatarAppearance == null)
                    otherAdvertise.ScriptedAvatarAppearance = advertise.ScriptedAvatarAppearance;
                if (string.IsNullOrEmpty(otherAdvertise.ScriptedAvatarOutfitId))
                    otherAdvertise.ScriptedAvatarOutfitId = advertise.ScriptedAvatarOutfitId;
                otherAvatar = EnsureScriptedAvatar(otherUid, otherAdvertise);
            }

            PlayScriptedMessageOnHolopad(otherUid, otherHolopad, otherAdvertise, otherAvatar, step, senderName, voiceId);
        }
    }

    private void PlayScriptedMessageOnHolopad(
        EntityUid uid,
        HolopadComponent holopad,
        HolopadAdvertiseComponent advertise,
        EntityUid? avatar,
        HolopadScriptedMessageStep step,
        string senderName,
        string? voiceId)
    {
        if (string.IsNullOrWhiteSpace(step.Message))
            return;

        var entity = new Entity<HolopadComponent>(uid, holopad);
        if (holopad.Hologram == null)
            _holopad.GenerateHologram(entity);

        if (holopad.Hologram == null)
            return;

        var hologram = holopad.Hologram.Value.Owner;
        if (avatar != null && Exists(avatar.Value))
        {
            holopad.Hologram.Value.Comp.LinkedEntity = avatar.Value;
            Dirty(holopad.Hologram.Value);
        }

        if (!string.IsNullOrEmpty(voiceId))
        {
            if (!TryComp<TTSComponent>(hologram, out var tts))
                tts = AddComp<TTSComponent>(hologram);

            tts.VoicePrototypeId = voiceId;
            tts.Enabled = true;
            Dirty(hologram, tts);
        }

        _chatSystem.TrySendInGameICMessage(
            hologram,
            step.Message,
            InGameICChatType.Speak,
            ChatTransmitRange.Normal,
            hideLog: true,
            shell: null,
            player: null,
            nameOverride: senderName,
            checkRadioPrefix: false,
            ignoreActionBlocker: true);

        if (!string.IsNullOrWhiteSpace(step.MusicPath))
        {
            var sound = new SoundPathSpecifier(step.MusicPath);
            _audio.PlayPvs(sound, uid, AudioParams.Default.WithVolume(step.MusicVolumeDb));
        }
    }
}
