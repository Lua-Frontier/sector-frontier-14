// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client._Lua.Company;
using Content.Client._Lua.Company.UI;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._Lua.UserInterface.Systems.Company;

[UsedImplicitly]
public sealed class CompanyMenuUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    private CompanyFactionsWindow? _window;

    private MenuButton? FactionsButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.FactionsButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);
        var sys = _systems.GetEntitySystem<CompanyClientSystem>();
        _window = UIManager.CreateWindow<CompanyFactionsWindow>();
        sys.SetWindow(_window);
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);
        _window.OnClose += () =>
        { FactionsButton?.SetClickPressed(false); };
        _window.OnOpen += () =>
        { FactionsButton?.SetClickPressed(true); };
        _input.SetInputCommand(ContentKeyFunctions.OpenCompanyFactionsMenu, InputCmdHandler.FromDelegate(_ => ToggleWindow()));
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window == null) return;
        if (_systems.TryGetEntitySystem<CompanyClientSystem>(out var sys)) sys.SetWindow(null);
        _window.Dispose();
        _window = null;
        CommandBinds.Unregister<CompanyMenuUIController>();
    }

    public void LoadButton()
    {
        if (FactionsButton == null) return;
        FactionsButton.OnPressed += FactionsButtonPressed;
    }

    public void UnloadButton()
    {
        if (FactionsButton == null) return;
        FactionsButton.OnPressed -= FactionsButtonPressed;
    }

    private void FactionsButtonPressed(ButtonEventArgs args)
    { ToggleWindow(); }

    private void ToggleWindow()
    {
        if (_window == null) return;
        FactionsButton?.SetClickPressed(!_window.IsOpen);
        if (_window.IsOpen) _window.Close();
        else _window.Open();
    }
}

