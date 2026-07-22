#nullable enable
using Content.Shared.Database;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{
    public abstract class SharedBwoinkSystem : EntitySystem
    {
        public static NetUserId SystemUserId { get; } = new NetUserId(Guid.Empty);

        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<BwoinkTextMessage>(OnBwoinkTextMessage);
        }

        protected virtual void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs)
        { }

        protected void LogBwoink(BwoinkTextMessage message)
        {
        }

        [Serializable, NetSerializable]
        public sealed class BwoinkTextMessage : EntityEventArgs
        {
            public DateTime SentAt { get; }

            public NetUserId UserId { get; }

            public NetUserId TrueSender { get; }
            public string Text { get; }

            public bool PlaySound { get; }

            public readonly bool AdminOnly;

            public BwoinkTextMessage(NetUserId userId, NetUserId trueSender, string text, DateTime? sentAt = default, bool playSound = true, bool adminOnly = false)
            {
                SentAt = sentAt ?? DateTime.Now;
                UserId = userId;
                TrueSender = trueSender;
                Text = text;
                PlaySound = playSound;
                AdminOnly = adminOnly;
            }
        }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkDiscordRelayUpdated : EntityEventArgs
    {
        public bool DiscordRelayEnabled { get; }

        public BwoinkDiscordRelayUpdated(bool enabled)
        {
            DiscordRelayEnabled = enabled;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkClientTypingUpdated : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public bool Typing { get; }

        public BwoinkClientTypingUpdated(NetUserId channel, bool typing)
        {
            Channel = channel;
            Typing = typing;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkPlayerTypingUpdated : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public string PlayerName { get; }
        public bool Typing { get; }

        public BwoinkPlayerTypingUpdated(NetUserId channel, string playerName, bool typing)
        {
            Channel = channel;
            PlayerName = playerName;
            Typing = typing;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkCloseConversationMessage : EntityEventArgs
    {
        public NetUserId Channel { get; }

        public BwoinkCloseConversationMessage(NetUserId channel)
        {
            Channel = channel;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkReopenConversationMessage : EntityEventArgs
    {
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkRateAdminMessage : EntityEventArgs
    {
        public ReputationVoteValue Value { get; }
        public string? Comment { get; }

        public BwoinkRateAdminMessage(ReputationVoteValue value, string? comment)
        {
            Value = value;
            Comment = comment;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkConversationStateMessage : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public bool Closed { get; }
        public bool CanRate { get; }
        public bool RatingSubmitted { get; }
        public bool HasAdminTarget { get; }
        public NetUserId AdminUserId { get; }
        public string AdminName { get; }

        public BwoinkConversationStateMessage(
            NetUserId channel,
            bool closed,
            bool canRate,
            bool ratingSubmitted,
            bool hasAdminTarget,
            NetUserId adminUserId,
            string adminName)
        {
            Channel = channel;
            Closed = closed;
            CanRate = canRate;
            RatingSubmitted = ratingSubmitted;
            HasAdminTarget = hasAdminTarget;
            AdminUserId = adminUserId;
            AdminName = adminName;
        }
    }
}
