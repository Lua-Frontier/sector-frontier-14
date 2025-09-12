/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Content.Shared._Lua.Finance.Events;
using System.Linq;

namespace Content.Client._Lua.Finance.UI;

public sealed partial class NFFinanceLoansWindow : DefaultWindow
{
    public GridContainer Rows = default!;
    public Button RefreshBtn = default!;
    public LineEdit Search = default!;
    public OptionButton Sort = default!;
    public Button TabLoans = default!;
    public Button TabDeposits = default!;
    public PanelContainer HeaderLoans = default!;
    public PanelContainer HeaderDeposits = default!;
    private bool _allowForceClose;

    public event Action? OnRefresh;
    public event Action? OnRefreshDeposits;
    public event Action<int>? OnForceCloseRequested;

    private bool _showDeposits;
    private FinanceLoanRow[] _allLoans = System.Array.Empty<FinanceLoanRow>();
    private FinanceDepositOverviewRow[] _allDeposits = System.Array.Empty<FinanceDepositOverviewRow>();

    public NFFinanceLoansWindow()
    {
        RobustXamlLoader.Load(this);
        Rows = FindControl<GridContainer>("Rows");
        RefreshBtn = FindControl<Button>("RefreshBtn");
        Search = FindControl<LineEdit>("Search");
        Sort = FindControl<OptionButton>("Sort");
        TabLoans = FindControl<Button>("TabLoans");
        TabDeposits = FindControl<Button>("TabDeposits");
        HeaderLoans = FindControl<PanelContainer>("HeaderLoans");
        HeaderDeposits = FindControl<PanelContainer>("HeaderDeposits");

        Sort.AddItem(Loc.GetString("finance-loans-sort-by-time"));
        Sort.AddItem(Loc.GetString("finance-loans-sort-by-amount"));
        Sort.AddItem(Loc.GetString("finance-loans-sort-by-name"));
        Sort.SelectId(0);
        RefreshBtn.OnPressed += _ => { if (_showDeposits) OnRefreshDeposits?.Invoke(); else OnRefresh?.Invoke(); };
        Search.OnTextChanged += _ => Rebuild();
        Sort.OnItemSelected += args => { Sort.SelectId(args.Id); Rebuild(); };
        TabLoans.OnPressed += _ => { _showDeposits = false; HeaderLoans.Visible = true; HeaderDeposits.Visible = false; Rebuild(); };
        TabDeposits.OnPressed += _ => { _showDeposits = true; HeaderLoans.Visible = false; HeaderDeposits.Visible = true; Rebuild(); };
    }

    public void ConfigureTabs(bool showLoans, bool showDeposits, bool allowForceClose)
    {
        TabLoans.Visible = showLoans;
        HeaderLoans.Visible = showLoans && !_showDeposits;
        TabDeposits.Visible = showDeposits;
        HeaderDeposits.Visible = showDeposits && _showDeposits;
        _allowForceClose = allowForceClose;
    }

    public void UpdateRows(FinanceLoanRow[] rows)
    {
        _allLoans = rows;
        if (!_showDeposits)
            Rebuild();
    }

    public void UpdateDepositRows(FinanceDepositOverviewRow[] rows)
    {
        _allDeposits = rows;
        if (_showDeposits)
            Rebuild();
    }

    private void Rebuild()
    {
        Rows.RemoveAllChildren();
        var query = Search.Text?.Trim() ?? string.Empty;
        if (!_showDeposits)
        {
            System.Collections.Generic.IEnumerable<FinanceLoanRow> filtered = _allLoans;
            if (!string.IsNullOrEmpty(query))
            {
                var q = query.ToUpperInvariant();
                filtered = filtered.Where(r => (r.Name?.ToUpperInvariant().Contains(q) ?? false)
                    || r.YupiCode.ToUpperInvariant().Contains(q));
            }

            filtered = Sort.SelectedId switch
            {
                1 => filtered.OrderByDescending(r => r.Principal),
                2 => filtered.OrderBy(r => r.Name),
                _ => filtered.OrderBy(r => r.SecondsUntilCharge < 0 ? int.MaxValue : r.SecondsUntilCharge)
            };

            foreach (var r in filtered)
            {
                Rows.AddChild(new Label { Text = r.Name });
                Rows.AddChild(new Label { Text = r.Principal.ToString() });
                Rows.AddChild(new Label { Text = r.SecondsUntilCharge < 0 ? "—" : Loc.GetString("finance-seconds-short", ("seconds", r.SecondsUntilCharge)) });
                Rows.AddChild(new Label { Text = r.YupiCode });
            }
        }
        else
        {
            System.Collections.Generic.IEnumerable<FinanceDepositOverviewRow> filtered = _allDeposits;
            if (!string.IsNullOrEmpty(query))
            {
                var q = query.ToUpperInvariant();
                filtered = filtered.Where(r => r.Name.ToUpperInvariant().Contains(q) || r.YupiCode.ToUpperInvariant().Contains(q));
            }

            filtered = Sort.SelectedId switch
            {
                1 => filtered.OrderByDescending(r => r.Principal + r.Accrued),
                2 => filtered.OrderBy(r => r.Name),
                _ => filtered.OrderBy(r => r.NextCapSeconds)
            };

            foreach (var r in filtered)
            {
                Rows.AddChild(new Label { Text = r.Name });
                Rows.AddChild(new Label { Text = $"{r.Principal}+{r.Accrued}" });
                Rows.AddChild(new Label { Text = Loc.GetString("finance-seconds-short", ("seconds", r.NextCapSeconds)) });
                Rows.AddChild(new Label { Text = Loc.GetString("finance-seconds-short", ("seconds", r.StopAtSeconds)) });
                Rows.AddChild(new Label { Text = r.RateModel switch { DepositRateModel.FixedApr => Loc.GetString("finance-rate-model-fixed"), DepositRateModel.FloatingApr => Loc.GetString("finance-rate-model-floating"), DepositRateModel.ProgressiveApr => Loc.GetString("finance-rate-model-progressive"), _ => r.RateModel.ToString() } });
                if (_allowForceClose)
                {
                    var btn = new Button { Text = "Закрыть" };
                    var depId = r.DepositId;
                    btn.OnPressed += _ => OnForceCloseRequested?.Invoke(depId);
                    Rows.AddChild(btn);
                }
                else
                {
                    Rows.AddChild(new Label { Text = r.YupiCode });
                }
            }
        }
    }
}
