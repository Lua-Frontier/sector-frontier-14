using Content.Shared.Backmen.Language;
using Robust.Shared.Prototypes;

namespace Content.Server.Backmen.Language;

[ByRefEvent]
public record struct DetermineEntityLanguagesEvent
{
    public EntityUid EntityUid { get; init; }

    public HashSet<ProtoId<LanguagePrototype>> SpokenLanguages = new();
    public HashSet<ProtoId<LanguagePrototype>> UnderstoodLanguages = new();

    public DetermineEntityLanguagesEvent() { }
}


