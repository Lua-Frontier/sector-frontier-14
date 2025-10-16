/*
 Feature: Client BUI for loan issuance console.
 Purpose: Allows privileged users to issue loans and see rating context.
 State handling: FinanceRatingState populates rating, FinanceIssueLoanResponseState shows outcome.
*/

using Content.Shared.UserInterface;
using Content.Shared._Lua.Finance.BUI;
using Content.Shared._Lua.Finance.Events;
using Robust.Client.UserInterface;
using Content.Client._Lua.Finance.UI; //Lua

namespace Content.Client._NF.Finance.BUI;

public sealed class NFFinanceIssuanceBoundUserInterface : BoundUserInterface
{
    public NFFinanceIssuanceBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
    {
    }

    private NFFinanceIssuanceWindow? _window;

    protected override void Open()
    {
        base.Open();
        if (_window != null)
        {
            _window.MoveToFront();
        }
        else
        {
            _window = new NFFinanceIssuanceWindow();
            _window.IssueRequested += amt => SendMessage(new FinanceIssueLoanRequestMessage(amt));
            _window.OnClose += () => { _window = null; };
            _window.OpenCentered();
        }
        // запросим рейтинг для заполнения
        SendMessage(new FinanceRatingQueryMessage(""));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        if (_window != null)
        {
            _window.Close();
            _window = null;
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null)
            return;
        switch (state)
        {
            case FinanceRatingState r:
                _window.UpdateRating(r);
                break;
            case FinanceIssueLoanResponseState res:
                _window.UpdateResult(res);
                if (res.Success)
                {
                    // Автообновление рейтинга после успешной выдачи
                    SendMessage(new FinanceRatingQueryMessage(""));
                }
                break;
        }
    }
}


