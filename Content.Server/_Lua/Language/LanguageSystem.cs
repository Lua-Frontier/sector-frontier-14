using System.Linq;
using Content.Server._Lua.Language.Events;
using Content.Shared._Lua.Language;
using Content.Shared._Lua.Language.Components;
using Content.Shared._Lua.Language.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Ghost;
using UniversalLanguageSpeakerComponent = Content.Shared._Lua.Language.Components.UniversalLanguageSpeakerComponent;

namespace Content.Server._Lua.Language;

public sealed partial class LanguageSystem : SharedLanguageSystem
{
    private EntityQuery<LanguageSpeakerComponent> _languageSpeakerQuery;
    private EntityQuery<UniversalLanguageSpeakerComponent> _universalLanguageSpeakerQuery;

    public override void Initialize()
    {
        base.Initialize();
        InitializeNet();

        SubscribeLocalEvent<LanguageSpeakerComponent, ComponentInit>(OnInitLanguageSpeakerLocal);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, MapInitEvent>(OnUniversalInit);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, ComponentShutdown>(OnUniversalShutdown);

        _languageSpeakerQuery = GetEntityQuery<LanguageSpeakerComponent>();
        _universalLanguageSpeakerQuery = GetEntityQuery<UniversalLanguageSpeakerComponent>();
    }

    private void OnUniversalShutdown(EntityUid uid, UniversalLanguageSpeakerComponent component, ComponentShutdown args)
    {
        RemoveLanguage(uid, UniversalPrototype);
    }

    private void OnUniversalInit(EntityUid uid, UniversalLanguageSpeakerComponent component, MapInitEvent args)
    {
        AddLanguage(uid, UniversalPrototype);
    }

    private void OnInitLanguageSpeakerLocal(EntityUid uid, LanguageSpeakerComponent component, ComponentInit args)
    {
        if (string.IsNullOrEmpty(component.CurrentLanguage))
            component.CurrentLanguage = component.SpokenLanguages.FirstOrDefault(UniversalPrototype);

        UpdateEntityLanguages(uid);
    }

    public bool CanUnderstand(Entity<LanguageSpeakerComponent?> listener, string language)
    {
        if (HasComp<GhostComponent>(listener))
            return true;

        if (language == UniversalPrototype || _universalLanguageSpeakerQuery.HasComp(listener))
            return true;

        if (!_languageSpeakerQuery.Resolve(listener, ref listener.Comp, logMissing: false))
            return false;

        return listener.Comp.UnderstoodLanguages.Contains(language);
    }

    public bool CanSpeak(Entity<LanguageSpeakerComponent?> speaker, string language)
    {
        if (_universalLanguageSpeakerQuery.HasComp(speaker))
            return true;

        if (!_languageSpeakerQuery.Resolve(speaker, ref speaker.Comp, logMissing: false))
            return false;

        return speaker.Comp.SpokenLanguages.Contains(language);
    }

    public LanguagePrototype GetLanguage(Entity<LanguageSpeakerComponent?> speaker)
    {
        if (!_languageSpeakerQuery.Resolve(speaker, ref speaker.Comp, logMissing: false)
            || string.IsNullOrEmpty(speaker.Comp.CurrentLanguage)
            || !_prototype.TryIndex(speaker.Comp.CurrentLanguage, out var proto))
            return Universal;

        return proto;
    }

    public List<ProtoId<LanguagePrototype>> GetSpokenLanguages(Entity<LanguageSpeakerComponent?> uid)
    {
        if (!_languageSpeakerQuery.Resolve(uid, ref uid.Comp, logMissing: false))
            return [];
        return uid.Comp.SpokenLanguages;
    }

    public List<ProtoId<LanguagePrototype>> GetUnderstoodLanguages(Entity<LanguageSpeakerComponent?> uid)
    {
        if (!_languageSpeakerQuery.Resolve(uid, ref uid.Comp, logMissing: false))
            return [];
        return uid.Comp.UnderstoodLanguages;
    }

    public void SetLanguage(EntityUid speaker, string language, LanguageSpeakerComponent? component = null)
    {
        if (!CanSpeak(speaker, language)
            || !Resolve(speaker, ref component)
            || component.CurrentLanguage == language)
            return;

        component.CurrentLanguage = language;
        RaiseLocalEvent(speaker, new Events.LanguagesUpdateEvent(), true);
    }

    public void AddLanguage(
        Entity<LanguageKnowledgeComponent?> uid,
        string language,
        bool addSpoken = true,
        bool addUnderstood = true)
    {
        EnsureComp<LanguageKnowledgeComponent>(uid, out uid.Comp);
        var speaker = EnsureComp<LanguageSpeakerComponent>(uid);

        if (addSpoken && !uid.Comp.SpokenLanguages.Contains(language))
            uid.Comp.SpokenLanguages.Add(language);

        if (addUnderstood && !uid.Comp.UnderstoodLanguages.Contains(language))
            uid.Comp.UnderstoodLanguages.Add(language);

        UpdateEntityLanguages((uid, speaker));
    }

    public void RemoveLanguage(
        Entity<LanguageKnowledgeComponent?> uid,
        string language,
        bool removeSpoken = true,
        bool removeUnderstood = true)
    {
        if (!Resolve(uid, ref uid.Comp, false))
            return;

        if (removeSpoken)
            uid.Comp.SpokenLanguages.Remove(language);

        if (removeUnderstood)
            uid.Comp.UnderstoodLanguages.Remove(language);

        UpdateEntityLanguages(uid.Owner);
    }

    public bool EnsureValidLanguage(Entity<LanguageSpeakerComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return false;

        if (!entity.Comp.SpokenLanguages.Contains(entity.Comp.CurrentLanguage ?? ""))
        {
            entity.Comp.CurrentLanguage = entity.Comp.SpokenLanguages.FirstOrDefault(UniversalPrototype);
            RaiseLocalEvent(entity, new Events.LanguagesUpdateEvent());
            return true;
        }

        return false;
    }

    public void UpdateEntityLanguages(Entity<LanguageSpeakerComponent?> entity)
    {
        if (!_languageSpeakerQuery.Resolve(entity, ref entity.Comp, logMissing: false))
            return;

        var ev = new DetermineEntityLanguagesEvent
        {
            EntityUid = entity
        };

        if (TryComp<LanguageKnowledgeComponent>(entity, out var knowledge))
        {
            foreach (var spoken in knowledge.SpokenLanguages)
                ev.SpokenLanguages.Add(spoken);
            foreach (var understood in knowledge.UnderstoodLanguages)
                ev.UnderstoodLanguages.Add(understood);
        }

        RaiseLocalEvent(entity, ref ev, false);
        RaiseLocalEvent(ref ev);

        entity.Comp.SpokenLanguages.Clear();
        entity.Comp.UnderstoodLanguages.Clear();
        entity.Comp.SpokenLanguages.AddRange(ev.SpokenLanguages);
        entity.Comp.UnderstoodLanguages.AddRange(ev.UnderstoodLanguages);

        if (!EnsureValidLanguage(entity))
            RaiseLocalEvent(entity, new Events.LanguagesUpdateEvent());
    }
}


