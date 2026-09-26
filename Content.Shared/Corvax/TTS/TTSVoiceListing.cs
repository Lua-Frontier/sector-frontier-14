using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using CCCVarsDef = Content.Shared.Corvax.CCCVars.CCCVars;

namespace Content.Shared.Corvax.TTS;

public static class TTSVoiceListing
{
    public static bool IsNttsBackend(IConfigurationManager cfg) =>
        cfg.GetCVar(CCCVarsDef.TTSNtts);

    public static bool MatchesBackend(TTSVoicePrototype voice, bool ntts) =>
        voice.Ntts == ntts;

    public static IEnumerable<TTSVoicePrototype> EnumerateForBackend(
        IPrototypeManager prototypes,
        bool ntts,
        bool roundStartOnly = true)
    {
        return prototypes.EnumeratePrototypes<TTSVoicePrototype>()
            .Where(v => MatchesBackend(v, ntts) && (!roundStartOnly || v.RoundStart));
    }

    public static string PickDefaultVoiceId(
        IPrototypeManager prototypes,
        bool ntts,
        Sex sex)
    {
        var match = EnumerateForBackend(prototypes, ntts)
            .Where(v => !v.SponsorOnly && HumanoidCharacterProfile.CanHaveVoice(v, sex))
            .OrderBy(v => v.ID)
            .FirstOrDefault();

        if (match is not null)
            return match.ID;

        if (ntts)
            return "Papich";

        return SharedHumanoidAppearanceSystem.DefaultSexVoice.TryGetValue(sex, out var legacy)
            ? legacy
            : SharedHumanoidAppearanceSystem.DefaultVoice;
    }
}
