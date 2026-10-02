using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP;

public static class ErpAudio
{
    public static readonly Dictionary<Gender, ProtoId<SoundCollectionPrototype>> MoanSounds = new()
    {
        { Gender.Male, new ProtoId<SoundCollectionPrototype>("MoansMale") },
        { Gender.Female, new ProtoId<SoundCollectionPrototype>("MoansFemale") },
    };

    public static readonly Dictionary<Gender, ProtoId<SoundCollectionPrototype>> OrgasmSounds = new()
    {
        { Gender.Male, new ProtoId<SoundCollectionPrototype>("OrgasmMale") },
        { Gender.Female, new ProtoId<SoundCollectionPrototype>("OrgasmFemale") },
    };
}
