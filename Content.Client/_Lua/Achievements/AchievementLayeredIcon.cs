// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Lua.Achievements;

public sealed class AchievementLayeredIcon : LayoutContainer
{
    public AchievementLayeredIcon()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void SetLayers(IReadOnlyList<SpriteSpecifier> layers, SpriteSystem sprite)
    {
        RemoveAllChildren();

        foreach (var layer in layers)
        {
            var rect = new TextureRect
            {
                Texture = sprite.Frame0(layer),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                HorizontalExpand = true,
                VerticalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
            };

            AddChild(rect);
            SetAnchorPreset(rect, LayoutPreset.Wide);
        }
    }

    public void SetModulate(Color? color)
    {
        ModulateSelfOverride = color;

        foreach (var child in Children)
        {
            if (child is TextureRect rect)
                rect.ModulateSelfOverride = color;
        }
    }
}
