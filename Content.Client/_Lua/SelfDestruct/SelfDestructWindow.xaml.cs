// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared._Lua.SelfDestruct;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Lua.SelfDestruct;

public sealed class SelfDestructWindow : DefaultWindow
{
    public event Action<int>? OnDigit;
    public event Action? OnClear;
    public event Action? OnConfirmWarning;
    public event Action? OnSavePin;
    public event Action? OnArm;

    public Label FirstStatusLabel = default!;
    public Label SecondStatusLabel = default!;
    public Button ConfirmWarningButton = default!;
    public Button SavePinButton = default!;
    public Button ArmButton = default!;
    public Button ClearButton = default!;
    public GridContainer KeypadGrid = default!;

    public SelfDestructWindow()
    {
        RobustXamlLoader.Load(this);
        Title = Loc.GetString("self-destruct-ui-title");
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        FirstStatusLabel = FindControl<Label>("FirstStatusLabel");
        SecondStatusLabel = FindControl<Label>("SecondStatusLabel");
        ConfirmWarningButton = FindControl<Button>("ConfirmWarningButton");
        SavePinButton = FindControl<Button>("SavePinButton");
        ArmButton = FindControl<Button>("ArmButton");
        ClearButton = FindControl<Button>("ClearButton");
        KeypadGrid = FindControl<GridContainer>("KeypadGrid");
        ConfirmWarningButton.OnPressed += _ => OnConfirmWarning?.Invoke();
        SavePinButton.OnPressed += _ => OnSavePin?.Invoke();
        ArmButton.OnPressed += _ => OnArm?.Invoke();
        ClearButton.OnPressed += _ => OnClear?.Invoke();
        for (var i = 1; i <= 9; i++)
        {
            var b = new Button { Text = i.ToString() };
            var capture = i;
            b.OnPressed += _ => OnDigit?.Invoke(capture);
            KeypadGrid.AddChild(b);
        }
        var zero = new Button { Text = "0" };
        zero.OnPressed += _ => OnDigit?.Invoke(0);
        var clear = new Button { Text = "C" };
        clear.OnPressed += _ => OnClear?.Invoke();
        KeypadGrid.AddChild(clear);
        KeypadGrid.AddChild(zero);
        var enter = new Button { Text = "E" };
        enter.OnPressed += _ => OnSavePin?.Invoke();
        KeypadGrid.AddChild(enter);
    }

    public void UpdateState(SelfDestructUiState s)
    {
        ConfirmWarningButton.Visible = s.Status == SelfDestructStatus.Warning;
        SavePinButton.Visible = s.Status == SelfDestructStatus.SetupPin;
        ClearButton.Visible = s.Status is SelfDestructStatus.SetupPin or SelfDestructStatus.AwaitPin;
        ArmButton.Visible = s.Status == SelfDestructStatus.ReadyToArm || s.Status == SelfDestructStatus.CountingDown;
        ArmButton.Disabled = !s.AllowArm;
        KeypadGrid.Visible = s.Status is SelfDestructStatus.SetupPin or SelfDestructStatus.AwaitPin;
        Title = Loc.GetString("self-destruct-ui-title");
        switch (s.Status)
        {
            case SelfDestructStatus.Warning:
                FirstStatusLabel.Text = Loc.GetString("self-destruct-ui-warning").Replace("\\n", "\n");
                SecondStatusLabel.Text = string.Empty; break;
            case SelfDestructStatus.SetupPin:
                FirstStatusLabel.Text = Loc.GetString("self-destruct-ui-setup");
                SecondStatusLabel.Text = Loc.GetString("self-destruct-ui-current-pin", ("code", s.EnteredText)); break;
            case SelfDestructStatus.AwaitPin:
                FirstStatusLabel.Text = Loc.GetString("self-destruct-ui-enter-pin", ("len", s.EnteredLength), ("max", s.MaxLength));
                SecondStatusLabel.Text = Loc.GetString("self-destruct-ui-current-pin", ("code", VisualizeCode(s.EnteredLength, s.MaxLength))); break;
            case SelfDestructStatus.ReadyToArm:
                FirstStatusLabel.Text = Loc.GetString("self-destruct-ui-ready");
                SecondStatusLabel.Text = Loc.GetString("self-destruct-ui-current-pin", ("code", VisualizeCode(s.EnteredLength, s.MaxLength))); break;
            case SelfDestructStatus.CountingDown:
                FirstStatusLabel.Text = Loc.GetString("self-destruct-ui-countdown", ("time", s.RemainingTime));
                SecondStatusLabel.Text = string.Empty; break;
            default:
                FirstStatusLabel.Text = string.Empty;
                SecondStatusLabel.Text = string.Empty; break;
        }
    }

    private static string VisualizeCode(int codeLength, int maxLength)
    {
        var stars = new string('*', codeLength);
        var blanksCount = Math.Max(0, maxLength - codeLength);
        var blanks = new string('_', blanksCount);
        return stars + blanks;
    }
}


