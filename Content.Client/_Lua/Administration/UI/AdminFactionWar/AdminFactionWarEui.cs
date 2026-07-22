using Content.Client.Eui;
using Content.Shared._Lua.Administration.FactionWar;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Lua.Administration.UI.AdminFactionWar;

[UsedImplicitly]
public sealed class AdminFactionWarEui : BaseEui
{
    private AdminFactionWarWindow? _window;

    public override void Opened()
    {
        base.Opened();
        _window = new AdminFactionWarWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.RefreshPressed += () => SendMessage(new AdminFactionWarEuiMsg.RefreshRequest());
        _window.DeclareWarPressed += (aggressor, defender, announcement) =>
            SendMessage(new AdminFactionWarEuiMsg.ForceDeclareRequest(aggressor, defender, announcement));
        _window.EndWarPressed += warId => SendMessage(new AdminFactionWarEuiMsg.ForceEndRequest(warId));
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window?.Dispose();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not AdminFactionWarEuiState warState || _window == null)
            return;

        _window.UpdateState(warState);
    }
}
