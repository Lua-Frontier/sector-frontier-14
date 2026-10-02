using Content.Shared._Arcane.ErpPanel;
using Robust.Client.UserInterface;

namespace Content.Client._Arcane.ErpPanel;

public sealed class ErpPanelWindowBUI(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ErpPanelWindow? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ErpPanelWindow>();
        _menu.OnSendEmote += (interaction, customArousal, customMoaning) => SendMessage(interaction, customArousal, customMoaning);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu is null)
            return;

        if (state is ErpPanelBuiState newState)
            _menu.SetTarget(newState.User, newState.Target);
    }

    public void SendMessage(string interaction, float customArousal, float customMoaning)
    {
        SendMessage(new ErpPanelSendMessage(interaction, customArousal, customMoaning));
    }
}
