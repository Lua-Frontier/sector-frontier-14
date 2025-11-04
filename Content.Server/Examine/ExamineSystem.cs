using System.Linq;
using Content.Server.Verbs;
using Content.Shared._Lua.ERP; // Lua
using Content.Shared.Lua.CLVar; // Lua
using Content.Shared._NF.Bank.Components;
using Content.Shared.DetailExaminable;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Shared.Configuration; // Lua

namespace Content.Server.Examine
{
    [UsedImplicitly]
    public sealed class ExamineSystem : ExamineSystemShared
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!; // Lua
        [Dependency] private readonly VerbSystem _verbSystem = default!;

        private readonly FormattedMessage _entityNotFoundMessage = new();
        private readonly FormattedMessage _entityOutOfRangeMessage = new();

        public override void Initialize()
        {
            base.Initialize();
            _entityNotFoundMessage.AddText(Loc.GetString("examine-system-entity-does-not-exist"));
            _entityOutOfRangeMessage.AddText(Loc.GetString("examine-system-cant-see-entity"));

            SubscribeNetworkEvent<ExamineSystemMessages.RequestExamineInfoMessage>(ExamineInfoRequest);
        }

        public override void SendExamineTooltip(EntityUid player, EntityUid target, FormattedMessage message, bool getVerbs, bool centerAtCursor)
        {
            if (!TryComp<ActorComponent>(player, out var actor))
                return;

            var session = actor.PlayerSession;

            SortedSet<Verb>? verbs = null;
            if (getVerbs)
                verbs = _verbSystem.GetLocalVerbs(target, player, typeof(ExamineVerb));

            var ev = new ExamineSystemMessages.ExamineInfoResponseMessage(
                GetNetEntity(target), 0, message, verbs?.ToList(), centerAtCursor
            );

            RaiseNetworkEvent(ev, session.Channel);
        }

        private void ExamineInfoRequest(ExamineSystemMessages.RequestExamineInfoMessage request, EntitySessionEventArgs eventArgs)
        {
            var player = eventArgs.SenderSession;
            var session = eventArgs.SenderSession;
            var channel = player.Channel;
            var entity = GetEntity(request.NetEntity);

            if (session.AttachedEntity is not {Valid: true} playerEnt
                || !Exists(entity))
            {
                RaiseNetworkEvent(new ExamineSystemMessages.ExamineInfoResponseMessage(
                    request.NetEntity, request.Id, _entityNotFoundMessage), channel);
                return;
            }

            if (!CanExamine(playerEnt, entity))
            {
                RaiseNetworkEvent(new ExamineSystemMessages.ExamineInfoResponseMessage(
                    request.NetEntity, request.Id, _entityOutOfRangeMessage, knowTarget: false), channel);
                return;
            }

            SortedSet<Verb>? verbs = null;
            if (request.GetVerbs)
                verbs = _verbSystem.GetLocalVerbs(entity, playerEnt, typeof(ExamineVerb));

            var text = GetExamineText(entity, player.AttachedEntity);

            if (_cfg.GetCVar(CLVars.IsERP)
                && TryComp<BankAccountComponent>(entity, out _)
                && TryComp<DetailExaminableComponent>(entity, out var detail))
            {
                AddERPStatusToMessage(text, detail.ERPStatus);
            }

            RaiseNetworkEvent(new ExamineSystemMessages.ExamineInfoResponseMessage(
                request.NetEntity, request.Id, text, verbs?.ToList()), channel);
        }

        private void AddERPStatusToMessage(FormattedMessage message, EnumERPStatus status)
        {
            message.PushNewline();

            switch (status)
            {
                case EnumERPStatus.FULL:
                    message.PushColor(Color.Green);
                    break;
                case EnumERPStatus.HALF:
                    message.PushColor(Color.Yellow);
                    break;
                default:
                    message.PushColor(Color.Red);
                    break;
            }

            string statusText = status switch
            {
                EnumERPStatus.HALF => Loc.GetString("humanoid-erp-status-half"),
                EnumERPStatus.FULL => Loc.GetString("humanoid-erp-status-full"),
                _ => Loc.GetString("humanoid-erp-status-no")
            };

            message.AddText(statusText);
            message.Pop();
        }
    }
}
