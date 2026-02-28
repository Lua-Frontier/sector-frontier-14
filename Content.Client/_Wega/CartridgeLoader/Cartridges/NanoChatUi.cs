using Content.Client.UserInterface.Fragments;
using Content.Shared._Wega.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client._Wega.CartridgeLoader.Cartridges;

public sealed partial class WegaNanoChatUi : UIFragment
{
    private NanoChatUiFragment? _fragment;
    private NanoChatAddContactPopup? _addContactPopup;
    private NanoChatDiscoveryPopup? _discoveryPopup;
    private NanoChatJoinGroupPopup? _joinGroupPopup;
    private NanoChatCreateGroupPopup? _createGroupPopup;

    public override Control GetUIFragmentRoot() => _fragment!;

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new NanoChatUiFragment();
        _addContactPopup = new NanoChatAddContactPopup();
        _discoveryPopup = new NanoChatDiscoveryPopup();
        _joinGroupPopup = new NanoChatJoinGroupPopup();
        _createGroupPopup = new NanoChatCreateGroupPopup();
        _fragment.InitializeEmojiPicker();
        _fragment.OpenAddContact += () => _addContactPopup.OpenCentered();
        _fragment.OpenDiscovery += () =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatRequestDiscoveryList())));
        };
        _fragment.SetVisibleInDiscovery += visible =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatSetVisibleInDiscovery(visible))));
        };
        _fragment.DiscoveryListReceived += list =>
        {
            _discoveryPopup.SetEntries(list);
            _discoveryPopup.OpenCentered();
        };
        _discoveryPopup.OnAddContact += (contactId, contactName) =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatAddContact(contactId, contactName))));
        };
        _fragment.JoinGroup += () => _joinGroupPopup.OpenCentered();
        _fragment.CreateGroup += () => _createGroupPopup.OpenCentered();
        _fragment.EraseChat += contactId =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatEraseContact(contactId))));
        };
        _fragment.LeaveChat += groupId =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatLeaveGroup(groupId))));
        };
        _fragment.OpenEmojiPicker += () => _fragment.OpenEmojiPickerInternal();
        _fragment.OnMutePressed += () =>
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatMuted())));
        _fragment.SetActiveChat += chatId =>
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatSetActiveChat(chatId))));
        _fragment.SendMessage += message =>
        {
            if (_fragment.ActiveChatId != null)
            {
                userInterface.SendMessage(new CartridgeUiMessage(
                    new NanoChatUiMessageEvent(new NanoChatSendMessage(
                        _fragment.ActiveChatId, message))));
            }
        };
        _addContactPopup.OnContactAdded += (contactId, contactName) =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatAddContact(contactId, contactName))));
        };
        _joinGroupPopup.OnGroupJoined += groupId =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatJoinGroup(groupId))));
        };
        _createGroupPopup.OnGroupCreated += groupName =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(
                new NanoChatUiMessageEvent(new NanoChatCreateGroup(groupName))));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is NanoChatUiState chatState)
            _fragment?.UpdateState(chatState);
    }
}
