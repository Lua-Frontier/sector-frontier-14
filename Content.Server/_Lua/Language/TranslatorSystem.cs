using System.Linq;
using Content.Server.Popups;
using Content.Server.PowerCell;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared._Lua.Language;
using Content.Shared._Lua.Language.Components;
using Content.Shared._Lua.Language.Systems;
using Content.Shared.PowerCell;
using Content.Shared._Lua.Language.Components.Translators;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._Lua.Language;

public sealed class TranslatorSystem : SharedTranslatorSystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IntrinsicTranslatorComponent, DetermineEntityLanguagesEvent>(OnDetermineLanguages);
        SubscribeLocalEvent<HoldsTranslatorComponent, DetermineEntityLanguagesEvent>(OnProxyDetermineLanguages);

        SubscribeLocalEvent<HandheldTranslatorComponent, EntGotInsertedIntoContainerMessage>(OnTranslatorInserted);
        SubscribeLocalEvent<HandheldTranslatorComponent, EntParentChangedMessage>(OnTranslatorParentChanged);
        SubscribeLocalEvent<HandheldTranslatorComponent, ActivateInWorldEvent>(OnTranslatorToggle);
        SubscribeLocalEvent<HandheldTranslatorComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
    }

    private void OnDetermineLanguages(EntityUid uid, IntrinsicTranslatorComponent component, DetermineEntityLanguagesEvent ev)
    {
        if (!component.Enabled
            || TerminatingOrDeleted(uid)
            || !TryComp<LanguageKnowledgeComponent>(uid, out var knowledge)
            || !_powerCell.HasActivatableCharge(uid))
            return;

        CopyLanguages(component, ev, knowledge);
    }

    private void OnProxyDetermineLanguages(EntityUid uid, HoldsTranslatorComponent component, DetermineEntityLanguagesEvent ev)
    {
        if (!TryComp<LanguageKnowledgeComponent>(uid, out var knowledge))
            return;

        foreach (var (translator, translatorComp) in component.Translators.ToArray())
        {
            // Always allow enabled handheld translators to work when held
            if (!translatorComp.Enabled)
                continue;

            if (!_containers.TryGetContainingContainer(translator, out var container) || container.Owner != uid)
            {
                component.Translators.RemoveWhere(it => it.Owner == translator);
                continue;
            }

            // Special case: IntergalacticTranslator understands all languages
            var protoId = MetaData(translator).EntityPrototype?.ID;
            if (protoId == "IntergalacticTranslator")
            {
                foreach (var lang in _prototype.EnumeratePrototypes<LanguagePrototype>())
                {
                    ev.UnderstoodLanguages.Add(lang.ID);
                }
                // Force output language to Intergalactic while translator is held
                if (TryComp<LanguageSpeakerComponent>(uid, out var speakComp))
                {
                    _language.SetLanguage(uid, SharedLanguageSystem.UniversalPrototype, speakComp);
                }
            }

            CopyLanguages(translatorComp, ev, knowledge);
        }
    }

    private void OnTranslatorInserted(EntityUid translator,
        HandheldTranslatorComponent component,
        EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.Owner is not { Valid: true } holder || !HasComp<LanguageSpeakerComponent>(holder))
            return;

        var intrinsic = EnsureComp<HoldsTranslatorComponent>(holder);
        intrinsic.Translators.Add((translator, component));

        _language.UpdateEntityLanguages(holder);

        // If it's an intergalactic translator, force-set active language to Intergalactic after languages refreshed
        var protoId = MetaData(translator).EntityPrototype?.ID;
        if (protoId == "IntergalacticTranslator" && TryComp<LanguageSpeakerComponent>(holder, out var speakComp))
        {
            Timer.Spawn(0, () => _language.SetLanguage(holder, SharedLanguageSystem.UniversalPrototype, speakComp));
        }
    }

    private void OnTranslatorParentChanged(EntityUid translator,
        HandheldTranslatorComponent component,
        EntParentChangedMessage args)
    {
        if (!HasComp<HoldsTranslatorComponent>(args.OldParent))
            return;

        Timer.Spawn(0,
            () =>
            {
                if (Exists(args.OldParent) && HasComp<LanguageSpeakerComponent>(args.OldParent))
                    _language.UpdateEntityLanguages(args.OldParent.Value);
            });
    }

    private void OnTranslatorToggle(EntityUid translator,
        HandheldTranslatorComponent translatorComp,
        ActivateInWorldEvent args)
    {
        if (!translatorComp.ToggleOnInteract)
            return;

        var hasPower = _powerCell.HasDrawCharge(translator);
        var isEnabled = !translatorComp.Enabled && hasPower;

        translatorComp.Enabled = isEnabled;
        _powerCell.SetDrawEnabled(translator, isEnabled);

        if (_containers.TryGetContainingContainer((translator, Transform(translator), MetaData(translator)),
                out var holderCont)
            && holderCont.Owner is var holder
            && TryComp<LanguageSpeakerComponent>(holder, out var languageComp))
        {
            var firstNewLanguage =
                translatorComp.SpokenLanguages.FirstOrDefault(it => !languageComp.SpokenLanguages.Contains(it));
            _language.UpdateEntityLanguages(holder);

            if (isEnabled && translatorComp.SetLanguageOnInteract && firstNewLanguage is { })
                _language.SetLanguage(holder, firstNewLanguage, languageComp);
        }

        OnAppearanceChange(translator, translatorComp);

        if (hasPower)
        {
            var loc = isEnabled ? "translator-component-turnon" : "translator-component-shutoff";
            var message = Loc.GetString(loc, ("translator", translator));
            _popup.PopupEntity(message, translator, args.User);
        }
    }

    private void OnPowerCellSlotEmpty(EntityUid translator,
        HandheldTranslatorComponent component,
        PowerCellSlotEmptyEvent args)
    {
        component.Enabled = false;
        _powerCell.SetDrawEnabled(translator, false);
        OnAppearanceChange(translator, component);

        if (_containers.TryGetContainingContainer(translator, out var holderCont) &&
            HasComp<LanguageSpeakerComponent>(holderCont.Owner))
            _language.UpdateEntityLanguages(holderCont.Owner);
    }

    private void CopyLanguages(BaseTranslatorComponent from, DetermineEntityLanguagesEvent to, LanguageKnowledgeComponent knowledge)
    {
        var addSpoken =
            CheckLanguagesMatch(from.RequiredLanguages, knowledge.SpokenLanguages, from.RequiresAllLanguages);
        var addUnderstood = CheckLanguagesMatch(from.RequiredLanguages,
            knowledge.UnderstoodLanguages,
            from.RequiresAllLanguages);

        if (addSpoken)
        {
            foreach (var language in from.SpokenLanguages)
            {
                to.SpokenLanguages.Add(language);
            }
        }

        if (addUnderstood)
        {
            foreach (var language in from.UnderstoodLanguages)
            {
                to.UnderstoodLanguages.Add(language);
            }
        }
    }

    public static bool CheckLanguagesMatch(ICollection<string> required, ICollection<string> provided, bool requireAll)
    {
        if (required.Count == 0)
            return true;

        return requireAll
            ? required.All(provided.Contains)
            : required.Any(provided.Contains);
    }
}
