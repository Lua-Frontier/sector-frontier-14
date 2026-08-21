// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Client._Lua.Styles;
using Content.Shared._Lua.Achievements;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Lua.Achievements;

public sealed class AchievementTreeNode : ContainerButton
{
    public const int NodeSize = 44;

    public string AchievementId { get; }
    public AchievementNodeState State { get; private set; }

    private readonly AchievementLayeredIcon _icon;
    private readonly PanelContainer _frame;
    private readonly Label? _progressLabel;

    private Color _bg = LunaWindowStyle.PanelBg;
    private Color _border = Color.FromHex("#3D3D3D");
    private float _borderThickness = 2f;

    public AchievementTreeNode(
        string achievementId,
        IReadOnlyList<SpriteSpecifier> iconLayers,
        SpriteSystem sprite,
        AchievementNodeState state,
        int progress = 0,
        int progressMax = 0)
    {
        AchievementId = achievementId;
        State = state;
        SetSize = MinSize = MaxSize = new Vector2(NodeSize, NodeSize);
        ToggleMode = true;
        MouseFilter = MouseFilterMode.Stop;
        StyleBoxOverride = new StyleBoxEmpty();

        _frame = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            PanelOverride = new StyleBoxEmpty(),
        };

        _icon = new AchievementLayeredIcon
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = progressMax > 1 && state != AchievementNodeState.Unlocked
                ? new Thickness(4, 4, 4, 10)
                : new Thickness(4),
        };
        _icon.SetLayers(iconLayers, sprite);

        _frame.AddChild(_icon);

        if (progressMax > 1 && state != AchievementNodeState.Unlocked)
        {
            _progressLabel = new Label
            {
                Text = $"{progress}/{progressMax}",
                Align = Label.AlignMode.Center,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Bottom,
                HorizontalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
                FontColorOverride = Color.FromHex("#C5D0DE"),
                FontOverride = LunaWindowStyle.FontSmall,
            };
            _frame.AddChild(_progressLabel);
        }

        AddChild(_frame);
        ApplyState(state);
        RefreshChrome();
    }

    public void ApplyState(AchievementNodeState state)
    {
        State = state;
        Disabled = state is AchievementNodeState.Hidden;
        RefreshChrome();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        RefreshChrome();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var scale = UIScale;
        var size = NodeSize * scale;
        var box = new UIBox2(0, 0, size, size);
        var inset = Math.Max(1f, _borderThickness * scale);

        handle.DrawRect(box, _border);
        handle.DrawRect(new UIBox2(inset, inset, size - inset, size - inset), _bg);

        base.Draw(handle);
    }

    private void RefreshChrome()
    {
        if (_frame == null)
            return;

        if (Pressed)
        {
            _bg = Color.FromHex("#1E2D3F");
            _borderThickness = 2.5f;
            _border = State switch
            {
                AchievementNodeState.Unlocked => Color.FromHex("#5FD68A"),
                AchievementNodeState.Available => Color.FromHex("#7DD4FF"),
                _ => Color.FromHex("#A8B4C8"),
            };
        }
        else
        {
            _bg = LunaWindowStyle.PanelBg;
            _borderThickness = 2f;
            _border = State switch
            {
                AchievementNodeState.Unlocked => Color.FromHex("#3A8E5B"),
                AchievementNodeState.Available => Color.FromHex("#519ABA"),
                _ => Color.FromHex("#3D3D3D"),
            };
        }

        _icon.SetModulate(Pressed
            ? State switch
            {
                AchievementNodeState.Unlocked or AchievementNodeState.Available => Color.White,
                _ => Color.FromHex("#BBBBBB"),
            }
            : State switch
            {
                AchievementNodeState.Unlocked or AchievementNodeState.Available => Color.White,
                _ => Color.FromHex("#555555"),
            });
    }
}
