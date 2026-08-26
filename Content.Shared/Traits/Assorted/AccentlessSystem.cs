using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Shared.Traits.Assorted;

/// <summary>
/// This handles removing accents when using the accentless trait.
/// </summary>
public sealed class AccentlessSystem : EntitySystem
{
    private static readonly ProtoId<SpeechVerbPrototype> DefaultSpeechVerb = "Default";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccentlessComponent, ComponentStartup>(RemoveAccents);
    }

    private void RemoveAccents(EntityUid uid, AccentlessComponent component, ComponentStartup args)
    {
        foreach (var accent in component.RemovedAccents.Values)
        {
            RemCompDeferred(uid, accent.Component.GetType());
        }

        if (!TryComp(uid, out SpeechComponent? speech) || speech.SpeechVerb == DefaultSpeechVerb)
            return;

        speech.SpeechVerb = DefaultSpeechVerb;
        Dirty(uid, speech);
    }
}
