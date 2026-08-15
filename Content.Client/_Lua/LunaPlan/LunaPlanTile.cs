// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client._Lua.Styles;
using Content.Client.Stylesheets;
using Content.Shared._Lua.LunaPlan;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Client._Lua.LunaPlan;

public sealed class LunaPlanTile : PanelContainer
{
    public LunaPlanTile(LunaPlanPrototype plan)
    {
        HorizontalExpand = true;
        Margin = new Thickness(0, 0, 0, 6);
        PanelOverride = LunaWindowStyle.Box(LunaWindowStyle.PanelBg, LunaWindowStyle.PanelBorder);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(8, 6),
            SeparationOverride = 2,
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };

        var toggle = new Button
        {
            Text = "▾",
            MinWidth = 28,
            MaxWidth = 28,
        };
        toggle.AddStyleClass(StyleNano.StyleClassButtonNavCompact);
        if (toggle.Label != null)
            toggle.Label.FontOverride = LunaWindowStyle.FontSmall;

        var title = new Label
        {
            Text = Loc.GetString(plan.Headline),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            ClipText = true,
        };
        title.FontOverride = LunaWindowStyle.FontBody;
        title.FontColorOverride = LunaWindowStyle.TextPrimary;

        header.AddChild(toggle);
        header.AddChild(title);
        root.AddChild(header);

        var summary = Loc.GetString(plan.Summary);
        var hasSummary = !string.IsNullOrWhiteSpace(summary);
        var hasLabels = plan.Labels.Count > 0;

        Label? preview = null;
        if (hasSummary)
        {
            preview = new Label
            {
                Text = MakePreview(summary),
                Margin = new Thickness(34, 4, 0, 0),
                HorizontalExpand = true,
                ClipText = true,
            };
            LunaWindowStyle.StyleSecondary(preview);
            root.AddChild(preview);
        }

        BoxContainer? labelsCollapsed = null;
        if (hasLabels)
        {
            labelsCollapsed = BuildLabels(plan.Labels, new Thickness(34, 4, 0, 0));
            root.AddChild(labelsCollapsed);
        }

        var details = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Visible = false,
            SeparationOverride = 4,
        };

        if (hasSummary)
        {
            var body = new Label
            {
                Margin = new Thickness(34, 4, 0, 2),
                HorizontalExpand = true,
            };
            LunaWindowStyle.StyleLoreBody(body, summary, 300f, LunaWindowStyle.TextSecondary);
            details.AddChild(body);
        }

        BoxContainer? labelsExpanded = null;
        if (hasLabels)
        {
            labelsExpanded = BuildLabels(plan.Labels, new Thickness(34, 0, 0, 2));
            details.AddChild(labelsExpanded);
        }

        root.AddChild(details);
        AddChild(root);

        if (!hasSummary && !hasLabels)
        {
            toggle.Visible = false;
        }
        else if (!hasSummary && hasLabels)
        {
            toggle.Visible = false;
            if (labelsExpanded != null)
                labelsExpanded.Visible = false;
        }
        else
        {
            toggle.OnPressed += _ =>
            {
                var open = !details.Visible;
                details.Visible = open;
                toggle.Text = open ? "▴" : "▾";

                if (preview != null)
                    preview.Visible = !open;

                if (labelsCollapsed != null)
                    labelsCollapsed.Visible = !open;
            };
        }
    }

    private static BoxContainer BuildLabels(List<LocId> labels, Thickness margin)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = margin,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };

        foreach (var labelId in labels)
        {
            var key = labelId.ToString();
            var (bg, fg) = ColorForLabel(key);

            var chip = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = bg,
                    BorderColor = fg.WithAlpha(0.35f),
                    BorderThickness = new Thickness(1),
                    ContentMarginLeftOverride = 6,
                    ContentMarginRightOverride = 6,
                    ContentMarginTopOverride = 2,
                    ContentMarginBottomOverride = 2,
                },
            };

            var text = new Label { Text = Loc.GetString(labelId) };
            LunaWindowStyle.StyleTiny(text);
            text.FontColorOverride = fg;
            chip.AddChild(text);
            row.AddChild(chip);
        }

        return row;
    }

    private static (Color Bg, Color Fg) ColorForLabel(string key)
    {
        uint hash = 2166136261;
        foreach (var c in key)
        {
            hash ^= c;
            hash *= 16777619;
        }

        var hue = (hash & 0xFFFF) / 65535f;
        var satBias = ((hash >> 16) & 0xFF) / 255f;
        var saturation = 0.42f + satBias * 0.28f;
        var fg = Color.FromHsl(new Vector4(hue, saturation, 0.72f, 1f));
        var bg = Color.FromHsl(new Vector4(hue, saturation * 0.75f, 0.16f, 1f));
        return (bg, fg);
    }

    private static string MakePreview(string text)
    {
        var firstLine = text.Split('\n')[0].Trim();
        const int limit = 30;
        return firstLine.Length <= limit
            ? firstLine
            : firstLine[..limit] + "...";
    }
}
