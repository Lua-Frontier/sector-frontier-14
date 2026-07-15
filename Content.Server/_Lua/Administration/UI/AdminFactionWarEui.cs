using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server._Lua.Company;
using Content.Shared._Lua.Administration.FactionWar;
using Content.Shared.Administration;
using Content.Shared.Eui;

namespace Content.Server._Lua.Administration.UI;

public sealed class AdminFactionWarEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly FactionWarSystem _factionWar;
    private string _statusText = string.Empty;

    public AdminFactionWarEui()
    {
        IoCManager.InjectDependencies(this);
        _factionWar = _entMan.System<FactionWarSystem>();
    }

    public override void Opened()
    {
        base.Opened();

        if (!EnsureAuthorized())
            return;

        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!EnsureAuthorized())
            return;

        switch (msg)
        {
            case AdminFactionWarEuiMsg.RefreshRequest:
                StateDirty();
                break;
            case AdminFactionWarEuiMsg.ForceDeclareRequest declare:
                if (_factionWar.ForceDeclareWar(declare.AggressorCompanyId, declare.DefenderCompanyId, Loc.GetString("company-war-admin-declared-by"), declare.AnnouncementText, out var declareError))
                    _statusText = Loc.GetString("admin-faction-war-status-declared", ("aggressor", _factionWar.GetDisplayName(declare.AggressorCompanyId)), ("defender", _factionWar.GetDisplayName(declare.DefenderCompanyId)));
                else
                    _statusText = declareError ?? Loc.GetString("admin-faction-war-status-declare-failed");

                StateDirty();
                break;
            case AdminFactionWarEuiMsg.ForceEndRequest end:
                if (_factionWar.ForceEndWar(end.WarId, Player.Name, out var endError))
                    _statusText = Loc.GetString("admin-faction-war-status-ended", ("warId", end.WarId));
                else
                    _statusText = endError ?? Loc.GetString("admin-faction-war-status-end-failed");

                StateDirty();
                break;
        }
    }

    public override EuiStateBase GetNewState()
    {
        return new AdminFactionWarEuiState(_factionWar.GetAllActiveWarOverviews(), _statusText);
    }

    private bool EnsureAuthorized()
    {
        if (_admins.HasAdminFlag(Player, AdminFlags.Admin))
            return true;

        Close();
        return false;
    }
}
