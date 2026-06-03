// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client._Lua.DonateShop.Systems;
using Content.Client._Lua.DonateShop.UI;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._Lua.DonateShop;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input.Binding;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._Lua.UserInterface.Systems.DonateShop;

[UsedImplicitly]
public sealed class DonateShopUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    private DonateShopSystem? _system;
    private DonateShopWindow? _window;
    private MenuButton? DonateShopButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.DonateShopButton;

    public void OnStateEntered(GameplayState state)
    {
        _system = _systems.GetEntitySystem<DonateShopSystem>();
        _window = UIManager.CreateWindow<DonateShopWindow>();
        _window.OnBuyPressed += OnBuyPressed;
        _window.OnClose += () => DonateShopButton?.SetClickPressed(false);
        _window.OnOpen += () => DonateShopButton?.SetClickPressed(true);
        _system.OnStateUpdated += OnStateUpdated;
        _system.RequestState();
        _input.SetInputCommand(ContentKeyFunctions.OpenDonateShopMenu, InputCmdHandler.FromDelegate(_ => ToggleDonateShopWindow()));
    }

    public void OnStateExited(GameplayState state)
    {
        if (_system != null)
        { _system.OnStateUpdated -= OnStateUpdated; }
        if (_window != null)
        {
            _window.OnBuyPressed -= OnBuyPressed;
            _window.Dispose();
            _window = null;
        }
        _system = null;
        CommandBinds.Unregister<DonateShopUIController>();
    }

    public void UnloadButton()
    {
        if (DonateShopButton == null) return;
        DonateShopButton.Disabled = true;
        DonateShopButton.OnPressed -= DonateShopButtonPressed;
    }

    public void LoadButton()
    {
        if (DonateShopButton == null) return;
        DonateShopButton.Visible = true;
        DonateShopButton.Disabled = false;
        DonateShopButton.OnPressed -= DonateShopButtonPressed;
        DonateShopButton.OnPressed += DonateShopButtonPressed;
        _system?.RequestState();
    }

    private void DonateShopButtonPressed(ButtonEventArgs args)
    { ToggleDonateShopWindow(); }

    private void ToggleDonateShopWindow()
    {
        if (_system == null || _window == null) return;
        if (_window.IsOpen)
        {
            _window.Close();
            return;
        }
        _system.RequestOpen();
        _window.OpenCentered();
    }

    private void OnBuyPressed(string listingId)
    { _system?.RequestBuy(listingId); }

    private void OnStateUpdated(DonateShopStateMessage state)
    {
        if (DonateShopButton != null)
        {
            DonateShopButton.Visible = true;
            DonateShopButton.Disabled = false;
        }
        _window?.UpdateState(state);
    }
}
