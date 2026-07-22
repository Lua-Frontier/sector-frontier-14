// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Lua.RoundEnd;

public sealed class EvacuationTimerUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IInputManager _input = default!;

    private EvacuationTimerPanel? _panel;
    private bool _active;
    private bool _initialPositionSet;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        _panel = new EvacuationTimerPanel
        {
            Visible = false,
        };
        _initialPositionSet = false;

        if (UIManager.ActiveScreen is { } screen)
            screen.AddChild(_panel);
    }

    private void OnScreenUnload()
    {
        if (_panel != null)
        {
            _panel.Orphan();
            _panel = null;
        }

        _active = false;
    }

    public void OnStateEntered(GameplayState state)
    {
        _active = true;
    }

    public void OnStateExited(GameplayState state)
    {
        _active = false;

        if (_panel != null)
            _panel.Visible = false;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_active || _panel == null)
            return;

        if (_panel.Parent != null)
        {
            if (_panel.Dragging)
            {
                var mousePos = _input.MouseScreenPosition.Position / _panel.UIScale;
                var clamped = Vector2.Clamp(mousePos, Vector2.Zero, _panel.Parent.Size);
                LayoutContainer.SetPosition(_panel, clamped - _panel.DragOffset);
            }
            else if (!_initialPositionSet && _panel.Width > 0)
            {
                var x = (_panel.Parent.Width - _panel.Width) / 2f;
                LayoutContainer.SetPosition(_panel, new Vector2(x, 10));
                _initialPositionSet = true;
            }
        }

        var timerSystem = _entMan.System<EvacuationTimerSystem>();
        if (timerSystem.ExpectedEvacuationTime is not { } expectedEvacuationTime)
        {
            _panel.Visible = false;
            return;
        }

        var remaining = expectedEvacuationTime - _timing.CurTime;
        if (remaining <= TimeSpan.Zero)
        {
            _panel.Visible = false;
            return;
        }

        _panel.Visible = true;
        _panel.UpdateTimer(remaining);
    }
}
