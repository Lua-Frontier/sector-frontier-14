using System.Text;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Shared.Language.Systems;

public abstract class SharedLanguageSystem : EntitySystem
{
    public static readonly ProtoId<LanguagePrototype> FallbackLanguagePrototype = "Intergalactic";

    public static readonly ProtoId<LanguagePrototype> UniversalPrototype = "Intergalactic";

    public static LanguagePrototype Universal { get; private set; } = default!;

    [Dependency] protected readonly IPrototypeManager _prototype = default!;
    [Dependency] protected readonly SharedGameTicker _ticker = default!;

    public override void Initialize()
    {
        Universal = _prototype.Index<LanguagePrototype>(UniversalPrototype);
    }

    public LanguagePrototype? GetLanguagePrototype(string id)
    {
        _prototype.TryIndex<LanguagePrototype>(id, out var proto);
        return proto;
    }

    public string ObfuscateSpeech(string message, LanguagePrototype language)
    {
        var builder = new StringBuilder();
        var method = language.Obfuscation;
        method.Obfuscate(builder, message, this);
        return builder.ToString();
    }
    internal int PseudoRandomNumber(int seed, int min, int max)
    {
        seed = seed ^ (_ticker.RoundId * 127);
        var random = seed * 1103515245 + 12345;
        return min + Math.Abs(random) % (max - min + 1);
    }
}


