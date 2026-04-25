using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Wega.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class NanoChatUiState : BoundUserInterfaceState
{
    public string ChatId;
    public string? ActiveChat;
    public bool Muted;
    public bool VisibleInDiscovery;
    public Dictionary<string, ChatContact> Contacts;
    public Dictionary<string, ChatGroup> Groups;
    public List<ChatMessage>? ActiveChatMessages;
    public List<DiscoveryEntry>? DiscoveryList;

    public NanoChatUiState(
        string chatId, string? activeChat, bool muted, bool visibleInDiscovery,
        Dictionary<string, ChatContact> contacts,
        Dictionary<string, ChatGroup> groups,
        List<ChatMessage>? activeChatMessages,
        List<DiscoveryEntry>? discoveryList = null
    )
    {
        ChatId = chatId;
        ActiveChat = activeChat;
        Muted = muted;
        VisibleInDiscovery = visibleInDiscovery;
        Contacts = contacts;
        Groups = groups;
        ActiveChatMessages = activeChatMessages;
        DiscoveryList = discoveryList;
    }
}

[Serializable, NetSerializable]
public sealed class DiscoveryEntry
{
    public string ChatId { get; }
    public string DisplayName { get; }
    public DiscoveryEntry(string chatId, string displayName)
    {
        ChatId = chatId;
        DisplayName = displayName;
    }
}

[Serializable, NetSerializable]
public sealed class ChatContact
{
    public string ContactId { get; }
    public string ContactName { get; }
    public bool HasUnread { get; }

    public ChatContact(string contactId, string contactName, bool hasUnread)
    {
        ContactId = contactId;
        ContactName = contactName;
        HasUnread = hasUnread;
    }
}

[Serializable, NetSerializable]
public sealed class ChatGroup
{
    public string GroupId { get; }
    public string GroupName { get; }
    public bool HasUnread { get; }
    public int MemberCount { get; }

    public ChatGroup(string groupId, string groupName, bool hasUnread, int memberCount)
    {
        GroupId = groupId;
        GroupName = groupName;
        HasUnread = hasUnread;
        MemberCount = memberCount;
    }
}

// Lua: добавлен [DataDefinition] для поддержки сохранения мира StarGate
[Serializable, NetSerializable, DataDefinition]
public sealed partial class ChatMessage
{
    [DataField]
    public string SenderId { get; set; } = string.Empty;
    [DataField]
    public string SenderName { get; set; } = string.Empty;
    [DataField]
    public string Message { get; set; } = string.Empty;
    [DataField]
    public TimeSpan Timestamp { get; set; }
    [DataField]
    public bool IsOwnMessage { get; set; }
    [DataField]
    public bool Delivered { get; set; }

    public ChatMessage() { }

    public ChatMessage(string senderId, string senderName, string message, TimeSpan timestamp, bool isOwnMessage, bool delivered)
    {
        SenderId = senderId;
        SenderName = senderName;
        Message = message;
        Timestamp = timestamp;
        IsOwnMessage = isOwnMessage;
        Delivered = delivered;
    }
}
