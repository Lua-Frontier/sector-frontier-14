using Content.Client.Eui;
using Content.Shared._Lua.Reputation;
using Content.Shared.Eui;

namespace Content.Client._Lua.Reputation;

public sealed class ReputationModerationEui : BaseEui
{
    private ReputationModerationWindow? _window;

    public override void Opened()
    {
        base.Opened();
        _window = new ReputationModerationWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.DeleteRequested += (voteId, reason) => SendMessage(new ReputationModerationDeleteVoteMessage(voteId, reason));
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
        if (state is not ReputationModerationEuiState reputationState || _window == null) return;
        _window.UpdateState(reputationState);
    }
}
