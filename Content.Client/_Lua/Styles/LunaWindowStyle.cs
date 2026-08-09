// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._Lua.Styles;

public static class LunaWindowStyle
{
    public static readonly Color FrameBg = Color.FromHex("#0B0F14");
    public static readonly Color FrameBorder = Color.FromHex("#243041");

    public static readonly Color TitleBarBg = Color.FromHex("#0E1218");
    public static readonly Color TitleBarBorder = Color.FromHex("#243041");

    public static readonly Color ContentBg = Color.FromHex("#0B0F14");
    public static readonly Color ContentBorder = Color.FromHex("#243041");

    public static readonly Color ShellBg = ContentBg;
    public static readonly Color ShellBorder = ContentBorder;

    public static readonly Color PanelBg = Color.FromHex("#0E1218");
    public static readonly Color PanelBorder = Color.FromHex("#2A3344");

    public static readonly Color Divider = Color.FromHex("#1C2433");
    public static readonly Color DividerStrong = Color.FromHex("#2A3344");

    public static readonly Color TextMuted = Color.FromHex("#7E8BA0");
    public static readonly Color TextSecondary = Color.FromHex("#8A96A8");
    public static readonly Color TextPrimary = Color.FromHex("#C5CEDB");

    public static readonly Color Accent = Color.FromHex("#5EC8E8");
    public static readonly Color AccentGood = Color.FromHex("#7DDBA3");
    public static readonly Color AccentWarn = Color.FromHex("#E8A43A");
    public static readonly Color AccentBad = Color.FromHex("#E07070");
    public static readonly Color AccentDrop = Color.FromHex("#7DDBA3");

    public static readonly Color ProgressCooldown = Color.FromHex("#3D9CC9");
    public static readonly Color ProgressConfirm = AccentWarn;
    public static readonly Color ProgressGood = Color.FromHex("#3CB371");
    public static readonly Color ProgressTrack = Color.FromHex("#1C2433");

    public static readonly Color OverlayDim = Color.FromHex("#0B0F14CC");

    public const int FontSizeTiny = 9;
    public const int FontSizeSmall = 10;
    public const int FontSizeBody = 12;
    public const int FontSizeTitle = 13;
    public const int FontSizeFooter = 8;
    public const float ProgressBarHeight = 14f;
    public const float ProgressBarHeightThick = 30f;

    private static Font? _fontTiny;
    private static Font? _fontSmall;
    private static Font? _fontBody;
    private static Font? _fontTitle;
    private static Font? _fontFooter;

    public static Font FontTiny => _fontTiny ??= Cache().NotoStack(size: FontSizeTiny);
    public static Font FontSmall => _fontSmall ??= Cache().NotoStack(size: FontSizeSmall);
    public static Font FontBody => _fontBody ??= Cache().NotoStack(size: FontSizeBody);
    public static Font FontTitle => _fontTitle ??= Cache().NotoStack(variation: "Bold", size: FontSizeTitle);
    public static Font FontFooter => _fontFooter ??= Cache().NotoStack(size: FontSizeFooter);

    private static IResourceCache Cache() => IoCManager.Resolve<IResourceCache>();

    public static StyleBoxFlat Frame(float border = 1f) => Box(FrameBg, FrameBorder, border);

    public static StyleBoxFlat Shell(float border = 1f) => Box(ContentBg, ContentBorder, border);

    public static StyleBoxFlat Panel(float border = 1f) => Box(PanelBg, PanelBorder, border);

    public static StyleBoxFlat TitleBar() => new()
    {
        BackgroundColor = TitleBarBg,
        BorderColor = TitleBarBorder,
        BorderThickness = new Thickness(0, 0, 0, 1),
    };

    public static StyleBoxFlat ThinDivider() => new()
    {
        BackgroundColor = Divider,
        BorderColor = Color.Transparent,
        BorderThickness = new Thickness(0),
        ContentMarginTopOverride = 1,
        ContentMarginBottomOverride = 1,
    };

    public static StyleBoxFlat VerticalDivider() => new()
    {
        BackgroundColor = DividerStrong,
        BorderColor = Color.Transparent,
        BorderThickness = new Thickness(0),
    };

    public static StyleBoxFlat DropHighlight() => Box(
        Color.FromHex("#122018"),
        AccentDrop,
        1f);

    public static StyleBoxFlat DimOverlay() => new()
    {
        BackgroundColor = OverlayDim,
        BorderColor = Color.Transparent,
        BorderThickness = new Thickness(0),
    };

    public static StyleBoxFlat Box(Color background, Color border, float borderThickness = 1f) => new()
    {
        BackgroundColor = background,
        BorderColor = border,
        BorderThickness = new Thickness(borderThickness),
    };

    public static void ApplyWindowChrome(FancyWindow window)
    {
        if (window.ChildCount < 2)
            return;

        if (window.GetChild(0) is PanelContainer frame)
        {
            frame.ModulateSelfOverride = Color.White;
            frame.PanelOverride = Frame();
        }

        if (window.GetChild(1) is not BoxContainer layout || layout.ChildCount < 2)
            return;

        if (layout.GetChild(0) is Control titleHost)
        {
            if (titleHost.ChildCount > 0 && titleHost.GetChild(0) is PanelContainer titlePanel)
            {
                titlePanel.ModulateSelfOverride = Color.White;
                titlePanel.PanelOverride = TitleBar();
            }

            if (titleHost.ChildCount > 1 &&
                titleHost.GetChild(1) is BoxContainer titleRow &&
                titleRow.ChildCount > 0 &&
                titleRow.GetChild(0) is Label titleLabel)
            {
                titleLabel.FontOverride = FontTitle;
                titleLabel.FontColorOverride = TextPrimary;
            }
        }

        if (layout.GetChild(1) is PanelContainer divider)
            divider.PanelOverride = ThinDivider();
    }

    public static void ApplyCompactStyle(Control root)
    {
        foreach (var child in root.Children)
        {
            switch (child)
            {
                case Label label:
                    label.AddStyleClass(StyleNano.StyleClassLabelSmall);
                    label.FontOverride = FontSmall;
                    break;
                case Button button:
                    button.AddStyleClass(StyleNano.StyleClassButtonNavCompact);
                    if (button.Label != null)
                        button.Label.FontOverride = FontSmall;
                    break;
                case Slider slider:
                    slider.AddStyleClass(StyleNano.StyleClassSliderThin);
                    break;
            }

            ApplyCompactStyle(child);
        }
    }

    public static void StyleHeading(Label label)
    {
        label.AddStyleClass(StyleNano.StyleClassLabelSmall);
        label.FontOverride = FontSmall;
        label.FontColorOverride = TextPrimary;
    }

    public static void StyleMuted(Label label)
    {
        label.AddStyleClass(StyleNano.StyleClassLabelSmall);
        label.FontOverride = FontSmall;
        label.FontColorOverride = TextMuted;
    }

    public static void StyleSecondary(Label label)
    {
        label.AddStyleClass(StyleNano.StyleClassLabelSmall);
        label.FontOverride = FontSmall;
        label.FontColorOverride = TextSecondary;
    }

    public static void StyleValue(Label label)
    {
        label.AddStyleClass(StyleNano.StyleClassLabelSmall);
        label.FontOverride = FontSmall;
        label.FontColorOverride = Accent;
    }

    public static void StyleDanger(Label label)
    {
        label.AddStyleClass(StyleNano.StyleClassLabelSmall);
        label.FontOverride = FontSmall;
        label.FontColorOverride = AccentBad;
    }

    public static void StyleFooter(Label label)
    {
        label.FontOverride = FontFooter;
        label.FontColorOverride = TextMuted;
    }

    public static void StyleTiny(Label label)
    {
        label.AddStyleClass(StyleNano.StyleClassLabelSmall);
        label.FontOverride = FontTiny;
        label.FontColorOverride = TextPrimary;
    }

    public static void StyleDivider(PanelContainer panel) => panel.PanelOverride = ThinDivider();
    public static void StyleVerticalDivider(PanelContainer panel) => panel.PanelOverride = VerticalDivider();

    public static StyleBoxFlat ProgressFill(Color fill) => new()
    {
        BackgroundColor = fill,
        BorderColor = Color.Transparent,
        BorderThickness = new Thickness(0),
    };

    public static StyleBoxFlat ProgressBackground() => new()
    {
        BackgroundColor = ProgressTrack,
        BorderColor = PanelBorder,
        BorderThickness = new Thickness(1),
    };

    public static void StyleProgressBar(ProgressBar bar, Color fill, float height = ProgressBarHeight)
    {
        bar.MinValue = 0f;
        bar.MaxValue = 1f;
        bar.SetHeight = height;
        bar.BackgroundStyleBoxOverride = ProgressBackground();
        bar.ForegroundStyleBoxOverride = ProgressFill(fill);
    }

    public static void StyleProgressCooldown(ProgressBar bar, float height = ProgressBarHeight) =>
        StyleProgressBar(bar, ProgressCooldown, height);

    public static void StyleProgressConfirm(ProgressBar bar, float height = ProgressBarHeight) =>
        StyleProgressBar(bar, ProgressConfirm, height);

    public static void StyleProgressGood(ProgressBar bar, float height = ProgressBarHeight) =>
        StyleProgressBar(bar, ProgressGood, height);
}
