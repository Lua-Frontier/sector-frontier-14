/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Content.Shared._Lua.Finance.Events;
using Robust.Client.Graphics;

namespace Content.Client._Lua.Finance.UI;

public sealed partial class NFFinanceIssuanceWindow : DefaultWindow
{
    // Левая колонка
    public Label Ident = default!;
    public ProgressBar ScoreBar = default!;
    public Label MaxLoan = default!;
    public Label ActiveLoans = default!;

    // Правая колонка
    public LineEdit Amount = default!;
    public Button IssueBtn = default!;
    public Label Validation = default!;
    public Label Banner = default!;

    public event Action<int>? IssueRequested;
    public event Action? RefreshRequested;

    public NFFinanceIssuanceWindow()
    {
        RobustXamlLoader.Load(this);
        Ident = FindControl<Label>("Ident");
        ScoreBar = FindControl<ProgressBar>("ScoreBar");
        MaxLoan = FindControl<Label>("MaxLoan");
        ActiveLoans = FindControl<Label>("ActiveLoans");
        Amount = FindControl<LineEdit>("Amount");
        IssueBtn = FindControl<Button>("IssueBtn");
        Validation = FindControl<Label>("Validation");
        Banner = FindControl<Label>("Banner");

        IssueBtn.OnPressed += _ =>
        {
            if (int.TryParse(Amount.Text, out var val) && val > 0)
                IssueRequested?.Invoke(val);
        };
        Amount.OnTextEntered += _ =>
        {
            if (int.TryParse(Amount.Text, out var val) && val > 0)
                IssueRequested?.Invoke(val);
        };
        // Кнопки обновления в этом окне больше нет; обновление выполняется из BUI
    }

    public void UpdateRating(FinanceRatingState s)
    {
        Ident.Text = string.IsNullOrWhiteSpace(s.TargetName) ? "—" : s.TargetName!;
        ScoreBar.Value = s.Score;
        // В этом окне показываем только прогрессбар без числового значения
        ColorizeBar(s.Score);
        MaxLoan.Text = FormatMoney(s.MaxLoan);
        ActiveLoans.Text = s.ActiveLoans.ToString();
    }

    public void UpdateResult(FinanceIssueLoanResponseState s)
    {
        Validation.Visible = false;
        Banner.Visible = false;

        if (s.Success)
        {
            Banner.Text = s.Message;
            Banner.Modulate = Color.FromHex("#5BBF6A");
            Banner.Visible = true;
        }
        else
        {
            Validation.Text = s.Message;
            Validation.Modulate = Color.FromHex("#D9534F");
            Validation.Visible = true;
        }
    }

    private void ColorizeBar(int score)
    {
        Color col;
        if (score < 40) col = Color.FromHex("#CC3A3A");
        else if (score < 70) col = Color.FromHex("#D8B84E");
        else col = Color.FromHex("#4CAF50");

        var fg = new StyleBoxFlat { BackgroundColor = col };
        var bg = new StyleBoxFlat { BackgroundColor = Color.FromHex("#333333") };
        ScoreBar.ForegroundStyleBoxOverride = fg;
        ScoreBar.BackgroundStyleBoxOverride = bg;
    }

    private static string FormatMoney(int value)
    {
        return string.Format("{0:N0}", value);
    }
}
