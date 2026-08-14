// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client._Lua.Info;
using Content.Client._Lua.Styles;
using Content.Client.UserInterface.Systems.Info;
using Content.Shared._Lua.Info;
using Content.Shared.Info;
using JetBrains.Annotations;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;

namespace Content.Client._Lua.UserInterface.Systems.Info;

[UsedImplicitly]
public sealed class PublicOfferUIController : UIController
{
    [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    private PanelContainer? _overlay;
    private PublicOfferWindow? _publicOfferWindow;
    private bool _awaitingOfferAcceptance;
    private bool _closingOfferIntentionally;
    private float _pendingRulesPopupTime;
    private string _pendingCoreRules = string.Empty;
    private bool _pendingShouldShowRules;

    protected override string SawmillName => "lua.public-offer";

    public bool ShouldDeferRulesDisplay => _awaitingOfferAcceptance;

    public override void Initialize()
    {
        base.Initialize();

        _netManager.RegisterNetMessage<PublicOfferAcceptedMessage>();
        _netManager.RegisterNetMessage<SendPublicOfferInformationMessage>(OnPublicOfferInformationMessage);
    }

    private void OnPublicOfferInformationMessage(SendPublicOfferInformationMessage message)
    {
        _pendingRulesPopupTime = message.PendingRulesPopupTime;
        _pendingCoreRules = message.PendingCoreRules;
        _pendingShouldShowRules = message.PendingShouldShowRules;
        _awaitingOfferAcceptance = true;
        ShowPublicOffer();
    }

    private void ShowPublicOffer()
    {
        EnsureOverlay();

        if (_publicOfferWindow != null)
            return;

        OpenPublicOfferWindow();
    }

    private void EnsureOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new PanelContainer
        {
            MouseFilter = Robust.Client.UserInterface.Control.MouseFilterMode.Stop,
            PanelOverride = LunaWindowStyle.DimOverlay(),
        };
        UIManager.WindowRoot.AddChild(_overlay);
        LayoutContainer.SetAnchorPreset(_overlay, LayoutContainer.LayoutPreset.Wide);
    }

    private void OpenPublicOfferWindow()
    {
        _publicOfferWindow = new PublicOfferWindow();
        _publicOfferWindow.OnAcceptPressed += OnPublicOfferAcceptPressed;
        _publicOfferWindow.OnDeclinePressed += OnPublicOfferDeclinePressed;
        _publicOfferWindow.OnClose += OnPublicOfferWindowClosed;
        UIManager.WindowRoot.AddChild(_publicOfferWindow);
        _publicOfferWindow.OpenCentered();
    }

    private void OnPublicOfferWindowClosed()
    {
        if (_closingOfferIntentionally || !_awaitingOfferAcceptance)
            return;

        _publicOfferWindow?.Dispose();
        _publicOfferWindow = null;
        OpenPublicOfferWindow();
    }

    private void ClosePublicOffer()
    {
        if (_publicOfferWindow == null)
        {
            RemoveOverlay();
            return;
        }

        _closingOfferIntentionally = true;
        _publicOfferWindow.OnClose -= OnPublicOfferWindowClosed;
        _publicOfferWindow.CloseAllowed();
        _publicOfferWindow.Dispose();
        _publicOfferWindow = null;
        _closingOfferIntentionally = false;

        RemoveOverlay();
    }

    private void RemoveOverlay()
    {
        _overlay?.Orphan();
        _overlay = null;
    }

    private void OnPublicOfferAcceptPressed()
    {
        _netManager.ClientSendMessage(new PublicOfferAcceptedMessage());

        ClosePublicOffer();
        _awaitingOfferAcceptance = false;

        if (_pendingShouldShowRules)
            UIManager.GetUIController<InfoUIController>().ShowRulesFromServer(_pendingCoreRules, _pendingRulesPopupTime);
    }

    private void OnPublicOfferDeclinePressed()
    {
        _consoleHost.ExecuteCommand("quit");
    }
}
