using Content.Shared._Lua.Language;
using Content.Shared._Lua.Language.Events;
using Content.Shared._Lua.Language.Systems;
using Robust.Client;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.Language.Systems;

public sealed class LanguageSystem : SharedLanguageSystem
{
    [Dependency] private readonly IBaseClient _client = default!;

    public ProtoId<LanguagePrototype> CurrentLanguage { get; private set; } = default!;
    public List<ProtoId<LanguagePrototype>> SpokenLanguages { get; private set; } = new();
    public List<ProtoId<LanguagePrototype>> UnderstoodLanguages { get; private set; } = new();

    public event EventHandler<LanguagesUpdatedMessage>? OnLanguagesChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LanguagesUpdatedMessage>(OnLanguagesUpdated);
        _client.RunLevelChanged += OnRunLevelChanged;
    }

    private void OnLanguagesUpdated(LanguagesUpdatedMessage message)
    {
        CurrentLanguage = message.CurrentLanguage;
        SpokenLanguages = message.Spoken;
        UnderstoodLanguages = message.Understood;
        OnLanguagesChanged?.Invoke(this, message);
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (args.NewLevel == ClientRunLevel.InGame)
            RequestStateUpdate();
    }

    public void RequestStateUpdate()
    {
        RaiseNetworkEvent(new RequestLanguagesMessage());
    }

    public void RequestSetLanguage(LanguagePrototype language)
    {
        if (language.ID == CurrentLanguage)
            return;

        RaiseNetworkEvent(new LanguagesSetMessage(language.ID));
        if (SpokenLanguages.Contains(language.ID))
            CurrentLanguage = language.ID;
    }
}


