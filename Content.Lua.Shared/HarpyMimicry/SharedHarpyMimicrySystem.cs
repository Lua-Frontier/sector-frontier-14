using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Speech.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Lua.Shared.HarpyMimicry;

public readonly record struct HarpyMimicOption(
    ProtoId<SpeciesPrototype>? Species,
    int? SoundIndex,
    LocId Name,
    EntProtoId Icon,
    SoundSpecifier Sound,
    AudioParams Params);

public readonly record struct HarpyOwnVoice(List<ResPath> Files, int Total, AudioParams Params);

public abstract class SharedHarpyMimicrySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public const int OwnVoiceIndex = -1;

    private static readonly ProtoId<SpeciesPrototype> BaselineSpecies = "Human";

    public List<HarpyMimicOption> GetOptions(Entity<HarpyMimicryComponent> ent, EmotePrototype emote)
    {
        var options = new List<HarpyMimicOption>();
        var seen = new HashSet<string>();
        var (sex, ownSpecies) = GetIdentity(ent);

        TryGetOwnSound(ent, sex, emote.ID, out var ownSound, out var ownSet);
        if (ownSound != null)
            seen.Add(SoundKey(ownSound));

        var species = _prototype.EnumeratePrototypes<SpeciesPrototype>()
            .Where(s => s.RoundStart && s.ID != ownSpecies)
            .OrderBy(s => s.ID != BaselineSpecies)
            .ThenBy(s => s.ID);

        foreach (var proto in species)
        {
            if (!TryGetSpeciesSound(proto, sex, emote.ID, out var sound, out var set))
                continue;

            if (!seen.Add(SoundKey(sound)))
                continue;

            options.Add(new HarpyMimicOption(proto.ID, null, proto.Name, proto.DollPrototype, sound, MergeParams(set, sound)));
        }

        if (ent.Comp.Sounds.TryGetValue(emote.ID, out var sounds))
        {
            for (var i = 0; i < sounds.Count; i++)
            {
                var entry = sounds[i];
                if (!seen.Add(SoundKey(entry.Sound)))
                    continue;

                options.Add(new HarpyMimicOption(null, i, entry.Name, entry.Icon, entry.Sound, MergeParams(ownSet, entry.Sound)));
            }
        }

        return options;
    }

    public HarpyOwnVoice GetOwnVoice(Entity<HarpyMimicryComponent> ent, EmotePrototype emote, List<HarpyMimicOption> options)
    {
        var (sex, _) = GetIdentity(ent);
        if (!TryGetOwnSound(ent, sex, emote.ID, out var sound, out var set))
            return new HarpyOwnVoice(new List<ResPath>(), 0, AudioParams.Default);

        var files = GetFiles(sound);
        var covered = options.SelectMany(option => GetFiles(option.Sound)).ToHashSet();
        var remaining = files.Where(file => !covered.Contains(file)).ToList();

        return new HarpyOwnVoice(remaining, files.Count, MergeParams(set, sound));
    }

    private (Sex Sex, ProtoId<SpeciesPrototype>? Species) GetIdentity(EntityUid uid)
    {
        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return (humanoid.Sex, humanoid.Species);

        return (Sex.Unsexed, null);
    }

    private bool TryGetOwnSound(
        EntityUid uid,
        Sex sex,
        string emoteId,
        [NotNullWhen(true)] out SoundSpecifier? sound,
        out EmoteSoundsPrototype? set)
    {
        sound = null;
        set = null;

        if (!TryComp<VocalComponent>(uid, out var vocal)
            || vocal.Sounds == null
            || !vocal.Sounds.TryGetValue(sex, out var setId)
            || !_prototype.TryIndex(setId, out set))
        {
            return false;
        }

        return set.Sounds.TryGetValue(emoteId, out sound);
    }

    private bool TryGetSpeciesSound(
        SpeciesPrototype species,
        Sex sex,
        string emoteId,
        [NotNullWhen(true)] out SoundSpecifier? sound,
        [NotNullWhen(true)] out EmoteSoundsPrototype? set)
    {
        sound = null;
        set = null;

        if (!_prototype.TryIndex(species.Prototype, out var entity)
            || !entity.TryComp<VocalComponent>(out var vocal, EntityManager.ComponentFactory)
            || vocal.Sounds == null
            || !vocal.Sounds.TryGetValue(sex, out var setId)
            || !_prototype.TryIndex(setId, out set))
        {
            return false;
        }

        return set.Sounds.TryGetValue(emoteId, out sound);
    }

    private static AudioParams MergeParams(EmoteSoundsPrototype? set, SoundSpecifier sound)
    {
        return set?.GeneralParams?.AddVolume(sound.Params.Volume) ?? sound.Params;
    }

    private List<ResPath> GetFiles(SoundSpecifier sound)
    {
        return sound switch
        {
            SoundCollectionSpecifier { Collection: { } id } when _prototype.TryIndex<SoundCollectionPrototype>(id, out var collection)
                => collection.PickFiles.Distinct().ToList(),
            SoundPathSpecifier path => new List<ResPath> { path.Path },
            _ => new List<ResPath>(),
        };
    }

    private string SoundKey(SoundSpecifier sound)
    {
        var files = GetFiles(sound);
        return files.Count > 0
            ? string.Join('|', files.Select(file => file.ToString()).Order())
            : sound.ToString() ?? string.Empty;
    }
}
