// LuaCorp - This file is licensed under AGPLv3
using System.Numerics;
using Content.Lua.UIKit.Styles;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.Lua.Research;
public static class ResearchServerStatusDot
{
    public static PanelContainer Create(bool connected = false)
    {
        var dot = new PanelContainer
        {
            SetSize = new Vector2(10, 10),
            MinSize = new Vector2(10, 10),
            MaxSize = new Vector2(10, 10),
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalAlignment = Control.HAlignment.Center,
        };
        Set(dot, connected);
        return dot;
    }

    public static void Set(PanelContainer dot, bool connected)
    {
        var color = connected ? LunaWindowStyle.AccentGood : LunaWindowStyle.AccentBad;
        dot.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = color,
            BorderColor = Color.Black.WithAlpha(0.35f),
            BorderThickness = new Thickness(1),
        };
    }
    public static (ContainerButton Button, PanelContainer Dot) CreateSelectButton(
        string text,
        float minHeight = 28f,
        float? minWidth = null)
    {
        var button = new ContainerButton { MinHeight = minHeight };
        if (minWidth is { } width)
            button.MinWidth = width;

        button.AddStyleClass(ContainerButton.StyleClassButton);
        button.AddStyleClass(StyleNano.StyleClassButtonNavCompact);

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(8, 0),
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
        };

        var dot = Create();
        var label = new Label
        {
            Text = text,
            VerticalAlignment = Control.VAlignment.Center,
        };
        label.AddStyleClass(StyleNano.StyleClassLabelSmall);
        label.FontOverride = LunaWindowStyle.FontSmall;

        row.AddChild(dot);
        row.AddChild(label);
        button.AddChild(row);
        return (button, dot);
    }
    public static void StyleSelectButton(ContainerButton button)
    {
        button.AddStyleClass(ContainerButton.StyleClassButton);
        button.AddStyleClass(StyleNano.StyleClassButtonNavCompact);

        foreach (var child in button.Children)
        {
            if (child is not BoxContainer row)
                continue;

            foreach (var inner in row.Children)
            {
                if (inner is not Label label)
                    continue;

                label.AddStyleClass(StyleNano.StyleClassLabelSmall);
                label.FontOverride = LunaWindowStyle.FontSmall;
            }
        }
    }
}
