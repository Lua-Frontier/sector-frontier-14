using System.Collections.Generic;
using Content.Client._Lua.Announce;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared._Lua.Announce;
using Content.Shared._Lua.CCVar;
using Content.Shared._RMC14.Announce;
using Content.Shared.Chat;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Announce;

public sealed class AnnouncementControllerSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private AnnouncementDisplayPreference _preference;
    private Dictionary<string, AnnouncementDisplayPreference> _overrides = new();
    private Dictionary<string, AnnouncementLayoutOverride> _layoutOverrides = new();
    private AnnouncementOverlayUIController? _overlayController;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(LuaCCVars.AnnouncementStyle, OnPreferenceChanged, true);
        _cfg.OnValueChanged(LuaCCVars.AnnouncementStyleOverrides, OnOverridesChanged, true);
        _cfg.OnValueChanged(LuaCCVars.AnnouncementLayoutOverrides, OnLayoutOverridesChanged, true);
        SubscribeNetworkEvent<AnnouncementNetMessage>(OnAnnouncementMessage);
    }

    private void OnAnnouncementMessage(AnnouncementNetMessage msg, EntitySessionEventArgs args)
    {
        if (_cfg.GetCVar(LuaCCVars.AnnouncementMirrorChat))
            MirrorToChat(msg.Data);

        var preference = ResolveDisplayPreference(msg.Data.AnnouncementId);
        if (preference == AnnouncementDisplayPreference.Disabled)
        {
            ReleaseOverride(msg.Data.OverrideId);
            return;
        }

        if (_uiManager.GetUIController<AnnouncementOverlayUIController>() is not { } controller)
        {
            ReleaseOverride(msg.Data.OverrideId);
            return;
        }

        if (_overlayController != controller)
        {
            if (_overlayController != null)
                _overlayController.AnnouncementDone -= OnAnnouncementDone;
            _overlayController = controller;
            _overlayController.AnnouncementDone += OnAnnouncementDone;
        }

        if (AnnouncementDisplayResolver.TryResolve(msg.Data, preference, out var resolved))
        {
            AnnouncementLayoutResolver.Apply(resolved, ResolveLayoutOverride(msg.Data.AnnouncementId));

            if (!resolved.ShowSprite)
            {
                ReleaseOverride(resolved.OverrideId);
                resolved.OverrideId = 0;
            }

            controller.ShowAnnouncement(resolved);
        }
        else
        {
            ReleaseOverride(msg.Data.OverrideId);
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlayController != null)
        {
            _overlayController.AnnouncementDone -= OnAnnouncementDone;
            _overlayController = null;
        }
    }

    private void OnAnnouncementDone(uint overrideId)
    {
        ReleaseOverride(overrideId);
    }

    private void MirrorToChat(AnnouncementNetData data)
    {
        var message = string.Join('\n', data.Text);
        var sender = ResolveMirrorSender(data);
        var wrapped = Loc.GetString(
            "chat-manager-sender-announcement-wrap-message",
            ("sender", sender),
            ("message", FormattedMessage.EscapeText(message)));
        var chat = new ChatMessage(
            ChatChannel.Radio,
            message,
            wrapped,
            NetEntity.Invalid,
            null,
            false,
            null);
        _uiManager.GetUIController<ChatUIController>().ProcessChatMessage(chat, false);
    }

    private string ResolveMirrorSender(AnnouncementNetData data)
    {
        if (!string.IsNullOrWhiteSpace(data.TitleOverride))
            return data.TitleOverride;

        var preference = ResolveDisplayPreference(data.AnnouncementId);
        var presentation = AnnouncementPresentationCatalog.Resolve(data.AnnouncementId, preference);
        var title = presentation.Style.TitleConfig.Title;
        if (title is { } loc && !string.IsNullOrEmpty(loc.Id))
            return Loc.GetString(loc);

        return Loc.GetString("chat-manager-sender-announcement");
    }

    private void ReleaseOverride(uint overrideId)
    {
        if (overrideId == 0 || !_net.IsConnected)
            return;

        RaiseNetworkEvent(new AnnouncementPlaybackDoneMsg(overrideId));
    }

    private void OnPreferenceChanged(AnnouncementDisplayPreference preference)
    {
        _preference = preference;
    }

    private void OnOverridesChanged(string serializedOverrides)
    {
        _overrides = AnnouncementPreferenceOverrides.Parse(serializedOverrides);
    }

    private void OnLayoutOverridesChanged(string serializedOverrides)
    {
        _layoutOverrides = AnnouncementLayoutOverrides.Parse(serializedOverrides);
        UpdateCurrentAnnouncementPosition();
    }

    private void UpdateCurrentAnnouncementPosition()
    {
        var screen = _uiManager.ActiveScreen;
        if (screen == null)
            return;

        var overlay = screen.GetWidget<AnnouncementOverlayWidget>();
        if (overlay == null)
            return;

        foreach (var widget in overlay.Announcements)
        {
            if (widget.ActiveAnnouncement is not { } active)
                continue;

            var layout = ResolveLayoutOverride(active.Data.AnnouncementId);
            active.Data.ScreenPositionOverride = layout?.Clamp().ScreenPosition;
        }

        overlay.Reflow();
    }

    public AnnouncementDisplayPreference ResolveDisplayPreference(AnnouncementPreset announcementId)
    {
        var id = AnnouncementPresetCatalog.GetId(announcementId);

        if (_overrides.TryGetValue(id, out var preference))
            return preference;

        return _preference;
    }

    public AnnouncementLayoutOverride? ResolveLayoutOverride(AnnouncementPreset announcementId)
    {
        return _layoutOverrides.TryGetValue(AnnouncementPresetCatalog.GetId(announcementId), out var overrideValue)
            ? overrideValue
            : null;
    }

    public AnnouncementLayoutOverride? GetPresetLayoutOverride(AnnouncementPreset announcementId)
    {
        return _layoutOverrides.TryGetValue(AnnouncementPresetCatalog.GetId(announcementId), out var overrideValue)
            ? overrideValue
            : null;
    }
}
