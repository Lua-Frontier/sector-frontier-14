using System.Text;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Language.Systems;

public abstract class SharedLanguageSystem : EntitySystem
{
    /// <summary>
    ///     Язык по умолчанию, когда сущность внезапно получает способность говорить (fallback).
    /// </summary>
    [ValidatePrototypeId<LanguagePrototype>]
    public static readonly string FallbackLanguagePrototype = "Intergalactic";

    /// <summary>
    ///     Язык, понимающий все остальные языки. Не должен устанавливаться напрямую.
    /// </summary>
    [ValidatePrototypeId<LanguagePrototype>]
    public static readonly string UniversalPrototype = "Intergalactic";

    /// <summary>
    ///     Кэшированный экземпляр прототипа универсального языка.
    /// </summary>
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

    /// <summary>
    ///     Обфусцирует сообщение согласно заданному языку.
    /// </summary>
    public string ObfuscateSpeech(string message, LanguagePrototype language)
    {
        var builder = new StringBuilder();
        var method = language.Obfuscation;
        method.Obfuscate(builder, message, this);
        return builder.ToString();
    }

    /// <summary>
    ///     Стабильный псевдослучайный номер в диапазоне [min, max] для указанного seed с привязкой к номеру раунда.
    /// </summary>
    internal int PseudoRandomNumber(int seed, int min, int max)
    {
        seed = seed ^ (_ticker.RoundId * 127);
        var random = seed * 1103515245 + 12345;
        return min + Math.Abs(random) % (max - min + 1);
    }
}


