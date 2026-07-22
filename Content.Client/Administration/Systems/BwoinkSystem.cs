#nullable enable
using Content.Shared.Administration;
using Content.Shared.Database;
using JetBrains.Annotations;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Client.Administration.Systems
{
    [UsedImplicitly]
    public sealed class BwoinkSystem : SharedBwoinkSystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;

        public event EventHandler<BwoinkTextMessage>? OnBwoinkTextMessageRecieved;
        private (TimeSpan Timestamp, bool Typing) _lastTypingUpdateSent;

        protected override void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs)
        { OnBwoinkTextMessageRecieved?.Invoke(this, message); }

        public void Send(NetUserId channelId, string text, bool playSound, bool adminOnly)
        {
            RaiseNetworkEvent(new BwoinkTextMessage(channelId, channelId, text, playSound: playSound, adminOnly: adminOnly));
            SendInputTextUpdated(channelId, false);
        }

        public void CloseConversation(NetUserId channelId)
        { RaiseNetworkEvent(new BwoinkCloseConversationMessage(channelId)); }

        public void ReopenConversation()
        { RaiseNetworkEvent(new BwoinkReopenConversationMessage()); }

        public void RateAdmin(ReputationVoteValue value, string? comment)
        { RaiseNetworkEvent(new BwoinkRateAdminMessage(value, comment)); }

        public void SendInputTextUpdated(NetUserId channel, bool typing)
        {
            if (_lastTypingUpdateSent.Typing == typing && _lastTypingUpdateSent.Timestamp + TimeSpan.FromSeconds(1) > _timing.RealTime)
            { return; }
            _lastTypingUpdateSent = (_timing.RealTime, typing);
            RaiseNetworkEvent(new BwoinkClientTypingUpdated(channel, typing));
        }
    }
}
