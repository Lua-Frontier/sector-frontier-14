// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.RichText;
using Content.Client.Guidebook.Richtext;
using Content.Client.UserInterface.ControlExtensions;
using Content.Lua.UIKit.Styles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Lua.Client.Guidebook.Richtext;
public sealed class WebLink : BoxContainer, IDocumentTag
{
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Text", out var text) || !args.TryGetValue("Url", out var url))
            return false;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        var label = new Label
        {
            Text = text,
            MouseFilter = MouseFilterMode.Stop,
            FontOverride = LunaWindowStyle.FontBody,
            FontColorOverride = WebLinkTag.LinkColor,
            DefaultCursorShape = CursorShape.Hand,
            Margin = new Thickness(0, 2, 0, 6)
        };

        label.OnMouseEntered += _ => label.FontColorOverride = WebLinkTag.HoverColor;
        label.OnMouseExited += _ => label.FontColorOverride = WebLinkTag.LinkColor;
        label.OnKeyBindDown += argsEvent =>
        {
            if (argsEvent.Function != EngineKeyFunctions.UIClick)
                return;

            if (label.TryGetParentHandler<ILinkClickHandler>(out var handler))
                handler.HandleClick(url);
        };

        Orientation = LayoutOrientation.Horizontal;
        AddChild(label);
        control = this;
        return true;
    }
}

