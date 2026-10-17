using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Shared._Arcane.ERP;

public static class ErpAudio
{
    // Lua start: female sounds only for Female, male for everyone else
    private static readonly ProtoId<SoundCollectionPrototype> MoansMale = "MoansMale";
    private static readonly ProtoId<SoundCollectionPrototype> MoansFemale = "MoansFemale";
    private static readonly ProtoId<SoundCollectionPrototype> OrgasmMale = "OrgasmMale";
    private static readonly ProtoId<SoundCollectionPrototype> OrgasmFemale = "OrgasmFemale";

    public static ProtoId<SoundCollectionPrototype> GetMoanSounds(Gender gender)
        => gender == Gender.Female ? MoansFemale : MoansMale;

    public static ProtoId<SoundCollectionPrototype> GetOrgasmSounds(Gender gender)
        => gender == Gender.Female ? OrgasmFemale : OrgasmMale;
    // Lua end
}
