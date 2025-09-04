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
using Robust.Shared.Maths;
using Robust.Shared.Localization;

namespace Content.Client._Lua.Finance.UI;

public sealed partial class NFFinanceRatingWindow : DefaultWindow
{
	public Button CheckBtn = default!;
	private Label _ident = default!;
	private ProgressBar _scoreBar = default!;
	private Label _scoreVal = default!;
	private Label _maxLoan = default!;
	private Label _activeLoans = default!;
	private Label _nextCharge = default!;
	private Label _updated = default!;

	public event Action? QueryRequested;

	public NFFinanceRatingWindow()
	{
		RobustXamlLoader.Load(this);
		CheckBtn = FindControl<Button>("CheckBtn");
		_ident = FindControl<Label>("Ident");
		_scoreBar = FindControl<ProgressBar>("ScoreBar");
		_scoreVal = FindControl<Label>("ScoreVal");
		_maxLoan = FindControl<Label>("MaxLoan");
		_activeLoans = FindControl<Label>("ActiveLoans");
		_nextCharge = FindControl<Label>("NextCharge");
		_updated = FindControl<Label>("Updated");
		CheckBtn.OnPressed += _ => QueryRequested?.Invoke();
	}

	public void UpdateState(FinanceRatingState state)
	{
		_ident.Text = string.IsNullOrWhiteSpace(state.TargetName) ? "—" : state.TargetName!;
		_scoreBar.Value = state.Score;
		_scoreVal.Text = state.Score.ToString();
		ColorizeBar(state.Score);
		_maxLoan.Text = state.MaxLoan.ToString();
		_activeLoans.Text = state.ActiveLoans.ToString();
		_nextCharge.Text = FormatEta(state.NextChargeSeconds);
		_updated.Text = Loc.GetString("finance-updated", ("time", DateTime.Now.ToString("HH:mm:ss")));
	}

	private void ColorizeBar(int score)
	{
		// Градиент: 0-40 красный, 40-70 жёлтый, 70-100 зелёный
		Color col;
		if (score < 40) col = Color.FromHex("#CC3A3A");
		else if (score < 70) col = Color.FromHex("#D8B84E");
		else col = Color.FromHex("#4CAF50");

		var fg = new StyleBoxFlat { BackgroundColor = col };
		var bg = new StyleBoxFlat { BackgroundColor = Color.FromHex("#333333") };
		_scoreBar.ForegroundStyleBoxOverride = fg;
		_scoreBar.BackgroundStyleBoxOverride = bg;
	}

	private static string FormatEta(int? seconds)
	{
		if (seconds is null)
			return "—";
		var s = seconds.Value;
		if (s < 60) return $"{s} с";
		var m = s / 60; var r = s % 60;
		if (m < 60) return r == 0 ? $"{m} мин" : $"{m} мин {r} с";
		var h = m / 60; var m2 = m % 60;
		return m2 == 0 ? $"{h} ч" : $"{h} ч {m2} мин";
	}
}


