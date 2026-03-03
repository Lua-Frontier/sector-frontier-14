// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Lua.Stargate.PlanetQuest;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Lua.Stargate.PlanetQuest;

public sealed class PlanetQuestUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IInputManager _input = default!;

    private PlanetQuestPanel? _panel;
    private bool _active;
    private bool _initialPositionSet;

    private float _completedHideRemaining;

    private const float CompletedPanelDisplayDuration = 30f;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        _panel = new PlanetQuestPanel();
        _panel.Visible = false;
        _initialPositionSet = false;

        if (UIManager.ActiveScreen is { } screen)
        {
            screen.AddChild(_panel);
        }
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
        _completedHideRemaining = 0f;
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

        var questComp = GetPlayerPlanetQuest();

        if (questComp == null)
        {
            _panel.Visible = false;
            _completedHideRemaining = 0f;
            return;
        }

        if (questComp.Completed)
        {
            if (_completedHideRemaining <= 0f)
                _completedHideRemaining = CompletedPanelDisplayDuration;

            _panel.Visible = true;
            _panel.UpdateQuest(questComp);

            _completedHideRemaining -= args.DeltaSeconds;
            if (_completedHideRemaining <= 0f)
            {
                _panel.Visible = false;
                _completedHideRemaining = 0f;
            }
            return;
        }

        _completedHideRemaining = 0f;
        _panel.Visible = true;
        _panel.UpdateQuest(questComp);
    }

    private PlanetQuestComponent? GetPlayerPlanetQuest()
    {
        if (_player.LocalEntity is not { } playerEnt)
            return null;

        if (!_entMan.TryGetComponent<TransformComponent>(playerEnt, out var xform))
            return null;

        if (xform.MapUid is not { } mapUid)
            return null;

        _entMan.TryGetComponent<PlanetQuestComponent>(mapUid, out var quest);
        return quest;
    }
}
