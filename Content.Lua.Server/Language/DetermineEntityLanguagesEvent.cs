using Content.Lua.Shared.Language;
using Robust.Shared.Prototypes;
using Content.Shared.Language;

namespace Content.Lua.Server.Language;

[ByRefEvent]
public record struct DetermineEntityLanguagesEvent
{
    public EntityUid EntityUid { get; init; }

    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = new();
    public HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();

    public DetermineEntityLanguagesEvent() { }
}


