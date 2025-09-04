/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Content.Shared._Lua.Finance.Events;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Content.Shared._NF.Bank;
using static Content.Shared._Lua.Finance.Events.DepositTermType;
using static Content.Shared._Lua.Finance.Events.DepositRateModel;
using Robust.Shared.Localization; //Lua

namespace Content.Client._Lua.Finance.UI;

public sealed partial class NFFinanceDepositWindow : DefaultWindow
{
    public LineEdit OpenAmount = default!;
    public OptionButton Term = default!;
    public OptionButton Rate = default!;
    public LineEdit CloseId = default!;
    public LineEdit TopUpId = default!;
    public LineEdit TopUpAmount = default!;
    public LineEdit PartWithdrawId = default!;
    public LineEdit PartWithdrawAmount = default!;
    public Label Balance = default!;
    public Label Available = default!;
    public Label Due = default!;
    public Label Hold = default!;
    public Label Eta = default!;
    public Button OpenBtn = default!;
    public Button CloseBtn = default!;
    public Button TopUpBtn = default!;
    public Button PartWithdrawBtn = default!;
    public Button Preset10 = default!;
    public Button Preset50 = default!;
    public Button Preset100 = default!;
    public Button RefreshBtn = default!;
    public ItemList DepositsList = default!;

    public event Action<int, DepositTermType, DepositRateModel>? OpenRequested;
    public event Action<int>? CloseRequested;
    public event Action<int, int>? TopUpRequested;
    public event Action<int, int>? PartialWithdrawRequested;
    public event Action? RefreshRequested;

    public NFFinanceDepositWindow()
    {
        RobustXamlLoader.Load(this);
        OpenAmount = FindControl<LineEdit>("OpenAmount");
        Term = FindControl<OptionButton>("Term");
        Rate = FindControl<OptionButton>("Rate");
        CloseId = FindControl<LineEdit>("CloseId");
        TopUpId = FindControl<LineEdit>("TopUpId");
        TopUpAmount = FindControl<LineEdit>("TopUpAmount");
        PartWithdrawId = FindControl<LineEdit>("PartWithdrawId");
        PartWithdrawAmount = FindControl<LineEdit>("PartWithdrawAmount");
        Balance = FindControl<Label>("Balance");
        Available = FindControl<Label>("Available");
        Due = FindControl<Label>("Due");
        Hold = FindControl<Label>("Hold");
        Eta = FindControl<Label>("Eta");
        OpenBtn = FindControl<Button>("OpenBtn");
        CloseBtn = FindControl<Button>("CloseBtn");
        TopUpBtn = FindControl<Button>("TopUpBtn");
        PartWithdrawBtn = FindControl<Button>("PartWithdrawBtn");
        Preset10 = FindControl<Button>("Preset10");
        Preset50 = FindControl<Button>("Preset50");
        Preset100 = FindControl<Button>("Preset100");
        RefreshBtn = FindControl<Button>("RefreshBtn");
        DepositsList = FindControl<ItemList>("DepositsList");

        Term.AddItem(Loc.GetString("finance-deposit-term-short"), (int) Short);
        Term.AddItem(Loc.GetString("finance-deposit-term-long"), (int) Long);
        Term.OnItemSelected += args =>
        {
            Term.SelectId(args.Id);
        };
        Term.SelectId(0);

        Rate.AddItem(Loc.GetString("finance-deposit-rate-fixed"), (int) FixedApr);
        Rate.AddItem(Loc.GetString("finance-deposit-rate-floating"), (int) FloatingApr);
        Rate.AddItem(Loc.GetString("finance-deposit-rate-progressive"), (int) ProgressiveApr);
        Rate.OnItemSelected += args =>
        {
            Rate.SelectId(args.Id);
        };
        Rate.SelectId(0);
        OpenBtn.OnPressed += _ =>
        {
            if (int.TryParse(OpenAmount.Text, out var amt) && amt > 0)
            {
                var term = (DepositTermType) Term.SelectedId;
                var rate = (DepositRateModel) Rate.SelectedId;
                OpenRequested?.Invoke(amt, term, rate);
            }
        };
        CloseBtn.OnPressed += _ =>
        {
            if (int.TryParse(CloseId.Text, out var id) && id > 0)
                CloseRequested?.Invoke(id);
        };

        TopUpBtn.OnPressed += _ =>
        {
            if (int.TryParse(TopUpId.Text, out var id) && id > 0 && int.TryParse(TopUpAmount.Text, out var amt) && amt > 0)
                TopUpRequested?.Invoke(id, amt);
        };

        PartWithdrawBtn.OnPressed += _ =>
        {
            if (int.TryParse(PartWithdrawId.Text, out var id) && id > 0 && int.TryParse(PartWithdrawAmount.Text, out var amt) && amt > 0)
                PartialWithdrawRequested?.Invoke(id, amt);
        };

        Preset10.OnPressed += _ => OpenAmount.Text = "10000";
        Preset50.OnPressed += _ => OpenAmount.Text = "50000";
        Preset100.OnPressed += _ => OpenAmount.Text = "100000";

        RefreshBtn.OnPressed += _ => RefreshRequested?.Invoke();
        DepositsList.OnItemSelected += args =>
        {
            // текст из строки начинается с ID — выдернем до пробела
            var text = DepositsList[args.ItemIndex].Text ?? string.Empty;
            var idStr = text.Split(' ')[0];
            if (int.TryParse(idStr, out var id))
                CloseId.Text = id.ToString();
        };
    }

    public void UpdateState(FinanceStatusState state)
    {
        Balance.Text = BankSystemExtensions.ToSpesoString(state.Balance);
        Available.Text = BankSystemExtensions.ToSpesoString(state.Available);
        Due.Text = BankSystemExtensions.ToSpesoString(state.Due);
        Hold.Text = BankSystemExtensions.ToSpesoString(state.Hold);
        Eta.Text = state.NextChargeEta is { } ts ? FormatEta(ts) : "—";
        OpenAmount.PlaceHolder = Loc.GetString("finance-deposit-open-amount-placeholder", ("min", state.MinAmount), ("max", state.MaxAmount), ("step", state.StepAmount));
    }

    public void UpdateDeposits(FinanceDepositListState state)
    {
        DepositsList.Clear();
        foreach (var row in state.Rows)
        {
            var principal = BankSystemExtensions.ToSpesoString(row.Principal);
            var accrued = BankSystemExtensions.ToSpesoString(row.Accrued);
            var penalty = BankSystemExtensions.ToSpesoString(row.EarlyPenaltyPreview);
            // Формат (RU): "<ID> | <осн>+<%> | <ставка>% | до кап. ~Xs | стоп ~Ys | штраф ~Z"
            var text = Loc.GetString("finance-deposit-row-format",
                ("id", row.Id),
                ("principal", principal),
                ("accrued", accrued),
                ("apr", row.AprPercent.ToString("F1")),
                ("next", row.NextCapSeconds),
                ("stop", row.StopAtSeconds),
                ("penalty", penalty));
            DepositsList.AddItem(text);
        }
    }

    private static string FormatEta(TimeSpan eta)
    {
        if (eta.TotalHours >= 1)
            return $"через {(int)eta.TotalHours}ч {eta.Minutes}м {eta.Seconds}с";
        if (eta.TotalMinutes >= 1)
            return $"через {eta.Minutes}м {eta.Seconds}с";
        return $"через {eta.Seconds}с";
    }
}


