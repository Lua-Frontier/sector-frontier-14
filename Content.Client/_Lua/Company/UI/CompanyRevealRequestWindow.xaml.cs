using Content.Shared._Lua.Company;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Lua.Company.UI;

public sealed partial class CompanyRevealRequestWindow : DefaultWindow
{
    private readonly int _requestId;
    private readonly CompanyClientSystem _system;

    public CompanyRevealRequestWindow(CompanyRevealRequestEvent ev, CompanyClientSystem system)
    {
        RobustXamlLoader.Load(this);
        _requestId = ev.RequestId;
        _system = system;

        var requestText = FindControl<RichTextLabel>("RequestText");
        var acceptButton = FindControl<Button>("AcceptButton");
        var declineButton = FindControl<Button>("DeclineButton");

        requestText.SetMessage(Loc.GetString("company-reveal-window-text", ("requester", ev.RequesterName)));
        acceptButton.OnPressed += _ =>
        {
            _system.RespondRevealRequest(_requestId, true);
            Close();
        };
        declineButton.OnPressed += _ =>
        {
            _system.RespondRevealRequest(_requestId, false);
            Close();
        };
    }
}