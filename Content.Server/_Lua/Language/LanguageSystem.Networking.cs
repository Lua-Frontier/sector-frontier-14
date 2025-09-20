using Content.Server.Mind;
using Content.Shared._Lua.Language;
using Content.Shared._Lua.Language.Components;
using Content.Shared._Lua.Language.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._Lua.Language;

public sealed partial class LanguageSystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public void InitializeNet()
    {
        SubscribeNetworkEvent<LanguagesSetMessage>(OnClientSetLanguage);
        SubscribeNetworkEvent<RequestLanguagesMessage>((_, session) =>
            SendLanguageStateToClient(session.SenderSession));

        SubscribeLocalEvent<LanguageSpeakerComponent, Events.LanguagesUpdateEvent>((uid, comp, _) =>
            SendLanguageStateToClient(uid, comp));

        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>((uid, _, _) => SendLanguageStateToClient(uid));
        SubscribeLocalEvent<MindComponent, MindGotRemovedEvent>((_, _, args) =>
        {
            var userId = args.Mind.Comp.UserId;
            if (userId != null && _players.TryGetSessionById(userId.Value, out var session))
                SendLanguageStateToClient(session);
        });
    }

    private void OnClientSetLanguage(LanguagesSetMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } uid)
            return;

        var language = GetLanguagePrototype(message.CurrentLanguage);
        if (language == null || !CanSpeak(uid, language.ID))
            return;

        SetLanguage(uid, language.ID);
    }

    private void SendLanguageStateToClient(EntityUid uid, LanguageSpeakerComponent? comp = null)
    {
        if (!_players.TryGetSessionByEntity(uid, out var session))
            return;

        SendLanguageStateToClient(uid, session, comp);
    }

    private void SendLanguageStateToClient(ICommonSession session, LanguageSpeakerComponent? comp = null)
    {
        if (session.AttachedEntity is not { Valid: true } entity)
            return;

        SendLanguageStateToClient(entity, session, comp);
    }

    private void SendLanguageStateToClient(EntityUid uid,
        ICommonSession session,
        LanguageSpeakerComponent? component = null)
    {
        var message = !Resolve(uid, ref component, logMissing: false)
            ? new LanguagesUpdatedMessage(UniversalPrototype, [UniversalPrototype], [UniversalPrototype])
            : new LanguagesUpdatedMessage(component.CurrentLanguage ?? "",
                component.SpokenLanguages,
                component.UnderstoodLanguages);

        RaiseNetworkEvent(message, session);
    }
}


