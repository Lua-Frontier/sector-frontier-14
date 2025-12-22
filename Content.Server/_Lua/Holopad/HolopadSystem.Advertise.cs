// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Chat.Systems;
using Content.Server.Clothing.Systems;
using Content.Shared._Lua.Chat.Systems;
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

namespace Content.Server.Holopad;

public sealed partial class HolopadSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoidSystem = default!;

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
    private void OnInteractHand(EntityUid uid, HolopadComponent component, InteractHandEvent args)
    {
        if (component.ScriptedMessages.Count == 0) return;
        if (IsHolopadControlLocked((uid, component), args.User) || IsHolopadBroadcastOnCoolDown((uid, component))) return;
        if (!_accessReaderSystem.IsAllowed(args.User, uid)) return;
        StartScriptedBroadcast((uid, component), args.User);
        args.Handled = true;
    }

    private void UpdateScriptedBroadcasts()
    {
        if (_scriptedBroadcasts.Count == 0) return;
        var now = _timing.CurTime;
        var finished = new List<EntityUid>();
        foreach (var (uid, state) in _scriptedBroadcasts)
        {
            if (!TryComp<HolopadComponent>(uid, out var holopad) || holopad.ScriptedMessages.Count == 0)
            { finished.Add(uid); continue; }
            if (now < state.NextStepTime) continue;
            if (state.CurrentIndex >= holopad.ScriptedMessages.Count)
            {
                if (!state.Completed)
                {
                    state.Completed = true;
                    state.NextStepTime = now + TimeSpan.FromSeconds(holopad.ScriptedEndDelaySeconds);
                    _appearanceSystem.SetData(uid, TelephoneVisuals.Key, TelephoneState.EndingCall);
                }
                else
                { finished.Add(uid); } continue;
            }
            if (state.CurrentIndex < 0)
            { finished.Add(uid); continue; }
            var step = holopad.ScriptedMessages[state.CurrentIndex];
            RunScriptedMessage((uid, holopad), state.Actor, state.Avatar, step);
            state.CurrentIndex++;
            if (state.CurrentIndex < holopad.ScriptedMessages.Count)
            {
                var next = holopad.ScriptedMessages[state.CurrentIndex];
                state.NextStepTime = now + TimeSpan.FromSeconds(next.DelaySeconds);
            }
        }

        foreach (var uid in finished)
        {
            if (_scriptedBroadcasts.TryGetValue(uid, out var state))
            {
                if (state.Avatar != null && Exists(state.Avatar.Value)) QueueDel(state.Avatar.Value);
                foreach (var linkedUid in state.LinkedHolopads)
                {
                    if (!Exists(linkedUid)) continue;
                    if (!TryComp<HolopadComponent>(linkedUid, out var linkedComp)) continue;
                    var linkedEnt = new Entity<HolopadComponent>(linkedUid, linkedComp);
                    if (linkedComp.Hologram != null) DeleteHologram(linkedComp.Hologram.Value, linkedEnt);
                    SetHolopadAmbientState(linkedEnt, false);
                }
            }
            _scriptedBroadcasts.Remove(uid);
            if (TryComp<HolopadComponent>(uid, out var holopadEnd))
            {
                var ent = new Entity<HolopadComponent>(uid, holopadEnd);
                if (holopadEnd.Hologram != null) DeleteHologram(holopadEnd.Hologram.Value, ent);
                SetHolopadAmbientState(ent, false);
                _appearanceSystem.SetData(uid, TelephoneVisuals.Key, TelephoneState.Idle);
            }
        }
    }

    private void StartScriptedBroadcast(Entity<HolopadComponent> source, EntityUid? actor)
    {
        if (source.Comp.ScriptedMessages.Count == 0) return;
        var state = new ScriptedBroadcastState
        {
            Actor = actor, CurrentIndex = 0,
        };
        var first = source.Comp.ScriptedMessages[0];
        state.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(source.Comp.ScriptedStartDelaySeconds + first.DelaySeconds);
        state.Avatar = EnsureScriptedAvatar(source);
        _scriptedBroadcasts[source] = state;
        SetHolopadAmbientState(source, true);
        source.Comp.ControlLockoutOwner = actor;
        source.Comp.ControlLockoutStartTime = _timing.CurTime;
        Dirty(source);
        _appearanceSystem.SetData(source.Owner, TelephoneVisuals.Key, TelephoneState.Ringing);
    }

    private EntityUid? EnsureScriptedAvatar(Entity<HolopadComponent> source)
    {
        if (source.Comp.ScriptedAvatarProtoId == null) return null;
        var container = _container.EnsureContainer<Container>(source.Owner, "holopad-scripted-avatar");
        EntityUid? existing = null;
        foreach (var ent in container.ContainedEntities)
        { existing = ent; break; }
        if (existing != null && Exists(existing.Value)) return existing.Value;
        var coords = Transform(source).Coordinates;
        var avatar = Spawn(source.Comp.ScriptedAvatarProtoId, coords);
        ConfigureScriptedAvatarAppearance(avatar, source);
        if (!string.IsNullOrEmpty(source.Comp.ScriptedAvatarOutfitId))
        {
            var outfit = EntitySystem.Get<OutfitSystem>();
            outfit.SetOutfit(avatar, source.Comp.ScriptedAvatarOutfitId);
        }
        _container.Insert(avatar, container);
        return avatar;
    }

    private void ConfigureScriptedAvatarAppearance(EntityUid avatar, Entity<HolopadComponent> source)
    {
        if (source.Comp.ScriptedAvatarAppearance is not { } settings) return;
        var profile = HumanoidCharacterProfile.DefaultWithSpecies(settings.Species)
            .WithName(settings.Name)
            .WithAge(settings.Age)
            .WithSex(settings.Sex)
            .WithGender(settings.Gender)
            .WithVoice(settings.Voice);
        var markings = new List<Marking>();
        foreach (var marking in settings.Markings)
        { markings.Add(new Marking(marking.MarkingId, marking.MarkingColors)); }
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

    private void RunScriptedMessage(Entity<HolopadComponent> holopad, EntityUid? actor, EntityUid? avatar, HolopadScriptedMessageStep step)
    {
        if (string.IsNullOrWhiteSpace(step.Message)) return;
        var senderName = holopad.Comp.ScriptedSenderName ?? MetaData(holopad).EntityName;
        var voiceId = step.VoiceId ?? holopad.Comp.ScriptedVoiceId;
        PlayScriptedMessageOnHolopad(holopad, avatar, step, senderName, voiceId);
        if (!holopad.Comp.ScriptedBroadcastToSector) return;
        var query = AllEntityQuery<HolopadComponent>();
        while (query.MoveNext(out var uid, out var otherComp))
        {
            if (uid == holopad.Owner) continue;
            var other = new Entity<HolopadComponent>(uid, otherComp);
            if (_scriptedBroadcasts.TryGetValue(holopad.Owner, out var state)) state.LinkedHolopads.Add(uid);
            EntityUid? otherAvatar = null;
            if (holopad.Comp.ScriptedAvatarProtoId != null)
            {
                if (other.Comp.ScriptedAvatarProtoId == null) other.Comp.ScriptedAvatarProtoId = holopad.Comp.ScriptedAvatarProtoId;
                if (other.Comp.ScriptedAvatarAppearance == null) other.Comp.ScriptedAvatarAppearance = holopad.Comp.ScriptedAvatarAppearance;
                if (string.IsNullOrEmpty(other.Comp.ScriptedAvatarOutfitId)) other.Comp.ScriptedAvatarOutfitId = holopad.Comp.ScriptedAvatarOutfitId;
                otherAvatar = EnsureScriptedAvatar(other);
            }
            PlayScriptedMessageOnHolopad(other, otherAvatar, step, senderName, voiceId);
        }
    }

    private void PlayScriptedMessageOnHolopad(Entity<HolopadComponent> holopad, EntityUid? avatar, HolopadScriptedMessageStep step, string senderName, string? voiceId)
    {
        if (string.IsNullOrWhiteSpace(step.Message)) return;
        if (holopad.Comp.Hologram == null) GenerateHologram(holopad);
        if (holopad.Comp.Hologram == null) return;
        var hologram = holopad.Comp.Hologram.Value.Owner;
        if (avatar != null && Exists(avatar.Value))
        {
            holopad.Comp.Hologram.Value.Comp.LinkedEntity = avatar.Value;
            Dirty(holopad.Comp.Hologram.Value);
        }
        if (!string.IsNullOrEmpty(voiceId))
        {
            if (!TryComp<TTSComponent>(hologram, out var tts)) tts = AddComp<TTSComponent>(hologram);
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
            _audio.PlayPvs(sound, holopad, AudioParams.Default.WithVolume(step.MusicVolumeDb));
        }
    }
}

