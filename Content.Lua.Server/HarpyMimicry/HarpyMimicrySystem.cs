using Content.Lua.Shared.HarpyMimicry;
using Content.Server.Chat.Systems;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Lua.Server.HarpyMimicry;

public sealed class HarpyMimicrySystem : SharedHarpyMimicrySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<PlayHarpyMimicEmoteMessage>(OnPlayMimicEmote);
        SubscribeLocalEvent<HarpyMimicryComponent, EmoteEvent>(OnEmote, before: new[] { typeof(VocalSystem) });
    }

    private void OnPlayMimicEmote(PlayHarpyMimicEmoteMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player
            || !TryComp<HarpyMimicryComponent>(player, out var mimicry))
            return;

        if (!_prototype.TryIndex(msg.Emote, out var emote) || emote.ChatTriggers.Count == 0)
            return;

        var options = GetOptions((player, mimicry), emote);

        if (msg.Species == null && msg.SoundIndex == OwnVoiceIndex)
        {
            var own = GetOwnVoice((player, mimicry), emote, options);
            if (own.Files.Count == 0)
                return;

            PlayWithSound(player, mimicry, emote, new SoundPathSpecifier(_random.Pick(own.Files)), own.Params);
            return;
        }

        HarpyMimicOption? chosen = null;
        foreach (var option in options)
        {
            var matches = msg.Species != null
                ? option.Species == msg.Species
                : option.Species == null && option.SoundIndex == msg.SoundIndex;

            if (!matches)
                continue;

            chosen = option;
            break;
        }

        if (chosen is not { } selected)
            return;

        PlayWithSound(player, mimicry, emote, selected.Sound, selected.Params);
    }

    private void PlayWithSound(EntityUid player, HarpyMimicryComponent mimicry, EmotePrototype emote, SoundSpecifier sound, AudioParams audioParams)
    {
        mimicry.Pending = (sound, audioParams);
        _chat.TryEmoteWithChat(player, emote);
        mimicry.Pending = null;
    }

    private void OnEmote(EntityUid uid, HarpyMimicryComponent component, ref EmoteEvent args)
    {
        if (args.Handled
            || component.Pending is not { } pending
            || !args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            return;

        _audio.PlayPvs(pending.Sound, uid, pending.Params);
        args.Handled = true;
    }
}
