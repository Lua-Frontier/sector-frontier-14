// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Company;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Lua.Company.UI;

public sealed partial class CompanyInviteWindow : DefaultWindow
{
    private readonly int _inviteId;
    private readonly CompanyClientSystem _system;

    public CompanyInviteWindow(CompanyInviteEvent ev, CompanyClientSystem system)
    {
        RobustXamlLoader.Load(this);
        _inviteId = ev.InviteId;
        _system = system;
        var inviteText = FindControl<RichTextLabel>("InviteText");
        var acceptButton = FindControl<Button>("AcceptButton");
        var declineButton = FindControl<Button>("DeclineButton");
        inviteText.SetMessage(Loc.GetString("company-invite-window-text", ("inviter", ev.InviterName), ("company", ev.CompanyName)));
        acceptButton.OnPressed += _ =>
        {
            _system.RespondInvite(_inviteId, true);
            Close();
        };
        declineButton.OnPressed += _ =>
        {
            _system.RespondInvite(_inviteId, false);
            Close();
        };
    }
}

