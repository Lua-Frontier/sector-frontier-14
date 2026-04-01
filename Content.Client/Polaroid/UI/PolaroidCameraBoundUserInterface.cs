using Content.Shared.DeadSpace.Polaroid;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.Polaroid.UI;

[UsedImplicitly]
public sealed class PolaroidCameraBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PolaroidCameraWindow? _window;

    public PolaroidCameraBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PolaroidCameraWindow>();
        _window.CaptureReady += OnCaptureReady;
        _window.PrintLastPressed += OnPrintLastPressed;
    }

    private void OnCaptureReady(byte[] png)
    {
        SendMessage(new PolaroidCaptureMessage(png));
    }

    private void OnPrintLastPressed()
    {
        SendMessage(new PolaroidPrintLastMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not PolaroidCameraUiState cast)
            return;

        _window?.SetState(cast);
    }
}
