// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using Content.Client.UserInterface.ControlExtensions;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.Guidebook.RichText;

[UsedImplicitly]
public sealed class WebLinkTag : IMarkupTagHandler
{
    public static Color LinkColor => Color.FromHex("#5EC8E8");
    public static Color HoverColor => Color.FromHex("#9AE0F5");

    public string Name => "weblink";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!node.Value.TryGetString(out var text)
            || !node.Attributes.TryGetValue("url", out var urlParameter)
            || !urlParameter.TryGetString(out var url))
        {
            control = null;
            return false;
        }

        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterMode.Stop,
            FontColorOverride = LinkColor,
            DefaultCursorShape = Control.CursorShape.Hand
        };

        label.OnMouseEntered += _ => label.FontColorOverride = HoverColor;
        label.OnMouseExited += _ => label.FontColorOverride = LinkColor;
        label.OnKeyBindDown += args => OnKeybindDown(args, url, label);

        control = label;
        return true;
    }

    private static void OnKeybindDown(GUIBoundKeyEventArgs args, string url, Control control)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (control.TryGetParentHandler<ILinkClickHandler>(out var handler))
            handler.HandleClick(url);
        else
            Logger.Warning("Warning! No valid ILinkClickHandler found for weblink.");
    }
}
