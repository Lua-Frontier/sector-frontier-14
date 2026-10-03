using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Lua.Shared.HarpyMimicry;

[Serializable, NetSerializable]
public sealed class PlayHarpyMimicEmoteMessage(ProtoId<EmotePrototype> emote, ProtoId<SpeciesPrototype>? species, int? soundIndex) : EntityEventArgs
{
    public readonly ProtoId<EmotePrototype> Emote = emote;
    public readonly ProtoId<SpeciesPrototype>? Species = species;
    public readonly int? SoundIndex = soundIndex;
}
