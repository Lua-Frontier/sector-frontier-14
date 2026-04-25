using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Server.PDA;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared._Wega.CartridgeLoader.Cartridges;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Wega.CartridgeLoader.Cartridges;

public sealed class NanoChatCartridgeSystem : SharedNanoChatCartridgeSystem
{
    private const int MaxContactIdLength = 5;
    private const int MaxContactNameLength = 9;
    private const int MaxGroupIdLength = 5;
    private const int MaxGroupNameLength = 16;
    private const int MaxMessageLength = 200;
    private const int MessageRange = 2000;

    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Maps ChatId (e.g. "#1234") to cartridge entity for delivery.</summary>
    private readonly Dictionary<string, EntityUid> _activeChats = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NanoChatCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<NanoChatCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<NanoChatCartridgeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NanoChatCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
        SubscribeLocalEvent<PdaComponent, OwnerNameChangedEvent>(OnPdaOwnerNameChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _activeChats.Clear();
    }

    private void OnPdaOwnerNameChanged(EntityUid uid, PdaComponent pda, ref OwnerNameChangedEvent args)
    {
        if (string.IsNullOrEmpty(pda.OwnerName))
            return;
        if (!_container.TryGetContainer(uid, SharedCartridgeLoaderSystem.InstalledContainerId, out var container))
            return;
        foreach (var cart in container.ContainedEntities)
        {
            if (TryComp<NanoChatCartridgeComponent>(cart, out var nanoChat))
                nanoChat.OwnerName = pda.OwnerName;
        }
    }

    private void OnMapInit(EntityUid uid, NanoChatCartridgeComponent component, MapInitEvent args)
    {
        component.ChatId = GenerateUniqueChatId();
        component.OwnerName = string.IsNullOrEmpty(component.OwnerName) ? Loc.GetString("generic-unknown-title") : component.OwnerName;
        _activeChats[component.ChatId] = uid;
    }

    private string GenerateUniqueChatId()
    {
        string id;
        do
        {
            id = "#" + _random.Next(10000).ToString("D4");
        } while (_activeChats.ContainsKey(id));
        return id;
    }

    private void OnCartridgeRemoved(EntityUid uid, NanoChatCartridgeComponent component, CartridgeRemovedEvent args)
    {
        _activeChats.Remove(component.ChatId);
    }

    private void OnUiReady(EntityUid uid, NanoChatCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        EnsureOwnerName(uid, component);
        UpdateUiState(uid, args.Loader, component, discoveryList: null);
    }

    private void OnUiMessage(EntityUid uid, NanoChatCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is not NanoChatUiMessageEvent message)
            return;

        var loaderUid = GetEntity(args.LoaderUid);
        EnsureOwnerName(uid, component);

        List<DiscoveryEntry>? discoveryList = null;

        switch (message.Payload)
        {
            case NanoChatAddContact add:
                AddContact(component, add.ContactId, add.ContactName);
                break;
            case NanoChatEraseContact erase:
                component.Contacts.Remove(erase.ContactId);
                component.Messages.Remove(erase.ContactId);
                if (component.ActiveChat == erase.ContactId)
                    component.ActiveChat = null;
                break;
            case NanoChatMuted:
                component.MutedSound = !component.MutedSound;
                break;
            case NanoChatSendMessage send:
                SendMessage(uid, component, send.RecipientId, send.Message);
                break;
            case NanoChatSetActiveChat set:
                component.ActiveChat = set.ContactId;
                ClearUnread(component, set.ContactId);
                break;
            case NanoChatCreateGroup create:
                CreateGroup(component, create.GroupName);
                break;
            case NanoChatJoinGroup join:
                JoinGroup(component, join.GroupId);
                break;
            case NanoChatLeaveGroup leave:
                LeaveGroup(component, leave.GroupId);
                if (component.ActiveChat == leave.GroupId)
                    component.ActiveChat = null;
                break;
            case NanoChatSetVisibleInDiscovery setVis:
                component.VisibleInDiscovery = setVis.Visible;
                break;
            case NanoChatRequestDiscoveryList:
                discoveryList = BuildDiscoveryList(component.ChatId);
                break;
        }

        UpdateUiState(uid, loaderUid, component, discoveryList);
    }

    private List<DiscoveryEntry> BuildDiscoveryList(string excludeChatId)
    {
        var list = new List<DiscoveryEntry>();
        foreach (var (chatId, cartUid) in _activeChats)
        {
            if (chatId == excludeChatId)
                continue;
            if (!TryComp<NanoChatCartridgeComponent>(cartUid, out var comp) || !comp.VisibleInDiscovery)
                continue;
            list.Add(new DiscoveryEntry(comp.ChatId, comp.OwnerName ?? chatId));
        }
        return list;
    }

    private void EnsureOwnerName(EntityUid uid, NanoChatCartridgeComponent component)
    {
        if (!string.IsNullOrEmpty(component.OwnerName))
            return;
        var root = Transform(uid).ParentUid;
        while (Transform(root).ParentUid.IsValid() && Transform(root).ParentUid != root)
            root = Transform(root).ParentUid;
        if (TryComp<PdaComponent>(root, out var pda) && !string.IsNullOrEmpty(pda.OwnerName))
            component.OwnerName = pda.OwnerName;
        else
            component.OwnerName = MetaData(root).EntityName;
    }

    private static void AddContact(NanoChatCartridgeComponent component, string contactId, string contactName)
    {
        var id = NormalizeContactId(Truncate(contactId, MaxContactIdLength));
        var name = Truncate(contactName, MaxContactNameLength);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            return;
        if (!component.Contacts.ContainsKey(id))
            component.Contacts[id] = new ChatContact(id, name, false);
    }

    private void CreateGroup(NanoChatCartridgeComponent component, string groupName)
    {
        var name = Truncate(groupName, MaxGroupNameLength);
        if (string.IsNullOrWhiteSpace(name))
            return;
        var id = "G" + NextGroupId(component);
        component.Groups[id] = new ChatGroup(id, name, false, 1);
    }

    private int NextGroupId(NanoChatCartridgeComponent component)
    {
        var used = component.Groups.Keys
            .Where(k => k.Length > 1 && k[0] == 'G' && int.TryParse(k.AsSpan(1), out _))
            .Select(k => int.TryParse(k.AsSpan(1), out var n) ? n : -1)
            .Where(n => n >= 0)
            .ToHashSet();
        for (var i = 0; i < 10000; i++)
        {
            if (!used.Contains(i))
                return i;
        }
        return _random.Next(0, 10000);
    }

    private static void JoinGroup(NanoChatCartridgeComponent component, string groupId)
    {
        var id = NormalizeGroupId(Truncate(groupId, MaxGroupIdLength));
        if (string.IsNullOrWhiteSpace(id))
            return;
        if (component.Groups.TryGetValue(id, out var group))
        {
            component.Groups[id] = new ChatGroup(id, group.GroupName, group.HasUnread, group.MemberCount + 1);
        }
        else
        {
            component.Groups[id] = new ChatGroup(id, id, false, 1);
        }
    }

    private static void LeaveGroup(NanoChatCartridgeComponent component, string groupId)
    {
        if (!component.Groups.TryGetValue(groupId, out var group))
            return;
        var newCount = Math.Max(0, group.MemberCount - 1);
        if (newCount == 0)
        {
            component.Groups.Remove(groupId);
            component.Messages.Remove(groupId);
        }
        else
        {
            component.Groups[groupId] = new ChatGroup(groupId, group.GroupName, group.HasUnread, newCount);
        }
    }

    private void SendMessage(EntityUid senderUid, NanoChatCartridgeComponent senderComp, string recipientId, string messageText)
    {
        var text = Truncate(messageText, MaxMessageLength);
        if (string.IsNullOrWhiteSpace(text))
            return;

        var now = _timing.CurTime;

        // Group chat: local only (no cross-cartridge groups in this implementation)
        if (recipientId.StartsWith("G"))
        {
            var msg = new ChatMessage(senderComp.ChatId, senderComp.OwnerName ?? "", text, now, true, true);
            if (!senderComp.Messages.ContainsKey(recipientId))
                senderComp.Messages[recipientId] = new List<ChatMessage>();
            senderComp.Messages[recipientId].Add(msg);
            MarkUnread(senderComp, recipientId);
            return;
        }

        // 1:1: deliver to recipient cartridge if in range
        if (!_activeChats.TryGetValue(recipientId, out var recipientUid) ||
            !TryComp<NanoChatCartridgeComponent>(recipientUid, out var recipientComp))
        {
            // Recipient not online: still add to sender's history
            var msg = new ChatMessage(senderComp.ChatId, senderComp.OwnerName ?? "", text, now, true, true);
            if (!senderComp.Messages.ContainsKey(recipientId))
                senderComp.Messages[recipientId] = new List<ChatMessage>();
            senderComp.Messages[recipientId].Add(msg);
            MarkUnread(senderComp, recipientId);
            return;
        }

        if (!IsWithinRange(senderUid, recipientUid))
        {
            var msg = new ChatMessage(senderComp.ChatId, senderComp.OwnerName ?? "", text, now, true, true);
            if (!senderComp.Messages.ContainsKey(recipientId))
                senderComp.Messages[recipientId] = new List<ChatMessage>();
            senderComp.Messages[recipientId].Add(msg);
            MarkUnread(senderComp, recipientId);
            return;
        }

        var senderMsg = new ChatMessage(senderComp.ChatId, senderComp.OwnerName ?? "", text, now, true, true);
        var recipientMsg = new ChatMessage(senderComp.ChatId, senderComp.OwnerName ?? "", text, now, false, true);

        if (!senderComp.Messages.ContainsKey(recipientId))
            senderComp.Messages[recipientId] = new List<ChatMessage>();
        senderComp.Messages[recipientId].Add(senderMsg);

        if (!recipientComp.Messages.ContainsKey(senderComp.ChatId))
            recipientComp.Messages[senderComp.ChatId] = new List<ChatMessage>();
        recipientComp.Messages[senderComp.ChatId].Add(recipientMsg);

        if (recipientComp.Contacts.TryGetValue(senderComp.ChatId, out var contact))
        {
            recipientComp.Contacts[senderComp.ChatId] = new ChatContact(contact.ContactId, contact.ContactName, true);
        }
        else
        {
            recipientComp.Contacts[senderComp.ChatId] = new ChatContact(senderComp.ChatId, senderComp.OwnerName ?? senderComp.ChatId, true);
        }

        if (TryComp<CartridgeComponent>(recipientUid, out _) && !recipientComp.MutedSound)
            _audio.PlayPvs(recipientComp.Sound, recipientUid);

        UpdateUiState(recipientUid, recipientComp, discoveryList: null);
    }

    private bool IsWithinRange(EntityUid sender, EntityUid recipient)
    {
        var senderCoords = _transform.GetMapCoordinates(sender);
        var recipientCoords = _transform.GetMapCoordinates(recipient);
        if (senderCoords.MapId != recipientCoords.MapId)
            return false;
        return senderCoords.InRange(recipientCoords, MessageRange);
    }

    private void UpdateUiState(EntityUid cartridgeUid, NanoChatCartridgeComponent component, List<DiscoveryEntry>? discoveryList)
    {
        if (!TryComp<CartridgeComponent>(cartridgeUid, out var cart) || !cart.LoaderUid.HasValue)
            return;
        UpdateUiState(cartridgeUid, cart.LoaderUid.Value, component, discoveryList);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, NanoChatCartridgeComponent component, List<DiscoveryEntry>? discoveryList)
    {
        List<ChatMessage>? activeMessages = null;
        if (!string.IsNullOrEmpty(component.ActiveChat) && component.Messages.TryGetValue(component.ActiveChat, out var list))
            activeMessages = list;

        var state = new NanoChatUiState(
            component.ChatId,
            component.ActiveChat,
            component.MutedSound,
            component.VisibleInDiscovery,
            new Dictionary<string, ChatContact>(component.Contacts),
            new Dictionary<string, ChatGroup>(component.Groups),
            activeMessages,
            discoveryList);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private static void MarkUnread(NanoChatCartridgeComponent component, string id)
    {
        if (component.Contacts.TryGetValue(id, out var c))
            component.Contacts[id] = new ChatContact(c.ContactId, c.ContactName, true);
        if (component.Groups.TryGetValue(id, out var g))
            component.Groups[id] = new ChatGroup(g.GroupId, g.GroupName, true, g.MemberCount);
    }

    private static void ClearUnread(NanoChatCartridgeComponent component, string id)
    {
        if (component.Contacts.TryGetValue(id, out var c))
            component.Contacts[id] = new ChatContact(c.ContactId, c.ContactName, false);
        if (component.Groups.TryGetValue(id, out var g))
            component.Groups[id] = new ChatGroup(g.GroupId, g.GroupName, false, g.MemberCount);
    }

    private static string Truncate(string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLen ? value : value[..maxLen];
    }

    private static string NormalizeContactId(string id)
    {
        var s = id.Trim().Replace(" ", "");
        if (s.Length > 0 && s[0] != '#' && s.All(char.IsDigit))
            return "#" + s;
        return s;
    }

    private static string NormalizeGroupId(string id)
    {
        var s = id.Trim().Replace(" ", "");
        if (s.Length > 0 && s[0] != 'G' && s.All(c => c == 'G' || char.IsDigit(c)))
            return "G" + s.TrimStart('G');
        if (s.All(char.IsDigit))
            return "G" + s;
        return s;
    }
}
