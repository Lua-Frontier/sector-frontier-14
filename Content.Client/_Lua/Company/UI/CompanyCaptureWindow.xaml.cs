// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Company;
using Robust.Client.Graphics;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Client._Lua.Company.UI;

public sealed partial class CompanyCaptureWindow : FancyWindow
{
    private readonly PanelContainer _capturePanel;
    private readonly BoxContainer _detailsContainer;
    private readonly Label _factionsLabel;
    private readonly Label _presenceLabel;
    private readonly Label _statusLabel;
    private readonly Label _progressLabel;
    private readonly ProgressBar _captureProgressBar;

    public CompanyCaptureWindow()
    {
        RobustXamlLoader.Load(this);

        var framePanel = (PanelContainer) GetChild(0);
        var windowLayout = (BoxContainer) GetChild(1);
        var titleHost = (Control) windowLayout.GetChild(0);
        var titlePanel = (PanelContainer) titleHost.GetChild(0);
        var dividerPanel = (PanelContainer) windowLayout.GetChild(1);

        _capturePanel = FindControl<PanelContainer>("CapturePanel");
        _detailsContainer = FindControl<BoxContainer>("DetailsContainer");
        _factionsLabel = FindControl<Label>("FactionsLabel");
        _presenceLabel = FindControl<Label>("PresenceLabel");
        _statusLabel = FindControl<Label>("StatusLabel");
        _progressLabel = FindControl<Label>("ProgressLabel");
        _captureProgressBar = FindControl<ProgressBar>("CaptureProgressBar");

        framePanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            BorderThickness = new Thickness(0),
        };

        titlePanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#10151D0D"),
            BorderColor = Color.Transparent,
            BorderThickness = new Thickness(0),
        };

        dividerPanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#5D6F8720"),
            BorderColor = Color.Transparent,
            BorderThickness = new Thickness(0),
            ContentMarginTopOverride = 1,
            ContentMarginBottomOverride = 1,
        };

        _capturePanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#10151D0D"),
            BorderColor = Color.FromHex("#5D6F8740"),
            BorderThickness = new Thickness(2),
            ContentMarginLeftOverride = 6,
            ContentMarginTopOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginBottomOverride = 6,
        };

        _captureProgressBar.BackgroundStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1C243099"),
        };

        _progressLabel.MinHeight = 12;
        _progressLabel.VerticalAlignment = VAlignment.Center;

        MinSize = new Vector2(560f, 160f);
        SetSize = new Vector2(560f, 160f);
    }

    public void UpdateState(CompanyCaptureStatusEvent state)
    {
        var progress = Math.Clamp(state.Progress, 0f, 1f);
        var progressPercent = (int)MathF.Round(progress * 100f);
        var activeColor = state.Paused ? Color.FromHex("#D08B36") : Color.FromHex("#3BC46B");
        var isUnownedStation = string.Equals(state.DefenderName, Loc.GetString("company-capture-unowned"), StringComparison.OrdinalIgnoreCase);

        Title = Loc.GetString("company-capture-window-title", ("station", state.StationName));
        _factionsLabel.Text = Loc.GetString(
            isUnownedStation ? "company-capture-window-factions-unowned" : "company-capture-window-factions",
            ("attacker", state.AttackerName),
            ("defender", state.DefenderName),
            ("station", state.StationName));
        _presenceLabel.Text = Loc.GetString("company-capture-window-presence", ("attackers", state.Attackers), ("required", state.RequiredAttackers), ("defenders", state.Defenders));
        _statusLabel.Text = Loc.GetString(state.Paused ? "company-capture-window-status-paused" : "company-capture-window-status-active");
        _statusLabel.FontColorOverride = activeColor;
        _progressLabel.Text = Loc.GetString("company-capture-window-progress-percent", ("value", progressPercent));
        _progressLabel.FontColorOverride = activeColor;
        _captureProgressBar.ForegroundStyleBoxOverride = new StyleBoxFlat(activeColor);
        _captureProgressBar.Value = progress;
    }
}
