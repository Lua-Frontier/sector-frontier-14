using Content.Shared._Lua.Autodoc;

namespace Content.Client._Lua.Autodoc;

public sealed class AutodocBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AutodocWindow? _window;

    public AutodocBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _window = new AutodocWindow();
        _window.OnSelectPart += part => SendMessage(new AutodocSelectPartMessage(part));
        _window.OnHealPart += part => SendMessage(new AutodocHealPartMessage(part));
        _window.OnRemovePart += part => SendMessage(new AutodocRemovePartMessage(part));
        _window.OnTransfer += SendMessage;
        _window.OnStop += () => SendMessage(new AutodocStopMessage());
        _window.OnClose += () => Close();
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is AutodocBoundUserInterfaceState autodocState)
            _window?.UpdateState(autodocState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
