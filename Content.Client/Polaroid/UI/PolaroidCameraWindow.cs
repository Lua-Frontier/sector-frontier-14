using System.IO;
using System.Numerics;
using Content.Client.Eye;
using Content.Client.Viewport;
using Content.Shared.DeadSpace.Polaroid;
using Robust.Client.Graphics;
using Robust.Client.Timing;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;

namespace Content.Client.DeadSpace.Polaroid.UI;

public sealed class PolaroidCameraWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IClientGameTiming _timing = default!;

    private readonly EyeLerpingSystem _eyeLerping = default!;
    private readonly FixedEye _defaultEye = new();
    private PolaroidCameraUiState? _nextState;
    private EntityUid? _currentCamera;
    private bool _disposed;

    private readonly ScalingViewport _cameraView;
    private readonly Label _chargesLabel;
    private readonly Button _captureButton;
    private readonly Button _printLastButton;

    public event Action<byte[]>? CaptureReady;
    public event Action? PrintLastPressed;

    public PolaroidCameraWindow()
    {
        IoCManager.InjectDependencies(this);
        _eyeLerping = _entManager.System<EyeLerpingSystem>();

        Title = Loc.GetString("polaroid-camera-ui-title");
        MinSize = new Vector2(320f, 420f);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };

        Contents.AddChild(root);

        var viewportFrame = new PanelContainer
        {
            MinSize = new Vector2(256f, 256f),
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Robust.Shared.Maths.Color.FromHex("#151515"),
                BorderColor = Robust.Shared.Maths.Color.FromHex("#d6d0c8"),
                BorderThickness = new Thickness(3),
                ContentMarginLeftOverride = 6,
                ContentMarginTopOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginBottomOverride = 6,
            }
        };

        root.AddChild(viewportFrame);

        _cameraView = new ScalingViewport
        {
            Eye = _defaultEye,
            ViewportSize = new Vector2i(160, 160),
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        viewportFrame.AddChild(_cameraView);

        _chargesLabel = new Label();
        root.AddChild(_chargesLabel);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };

        root.AddChild(buttons);

        _captureButton = new Button
        {
            Text = Loc.GetString("polaroid-camera-ui-capture"),
            HorizontalExpand = true,
        };

        _captureButton.OnPressed += _ => CaptureScreenshot();
        buttons.AddChild(_captureButton);

        _printLastButton = new Button
        {
            Text = Loc.GetString("polaroid-camera-ui-print-last"),
            HorizontalExpand = true,
        };

        _printLastButton.OnPressed += _ => PrintLastPressed?.Invoke();
        buttons.AddChild(_printLastButton);

        UpdateButtons();
    }

    public void SetState(PolaroidCameraUiState state)
    {
        _nextState = state;
        _cameraView.ViewportSize = new Vector2i(state.ViewportPixelSize, state.ViewportPixelSize);
        _chargesLabel.Text = Loc.GetString("polaroid-camera-ui-cartridge",
            ("current", state.CurrentCharges),
            ("max", state.MaxCharges));
        UpdateButtons();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_nextState == null || _timing.LastRealTick < _nextState.Tick)
            return;

        var preview = _entManager.GetEntity(_nextState.PreviewCamera);
        if (preview == null)
        {
            if (_currentCamera != null)
            {
                _eyeLerping.RemoveEye(_currentCamera.Value);
                _currentCamera = null;
            }

            _cameraView.Eye = _defaultEye;
            UpdateButtons();
            return;
        }

        if (_currentCamera == null)
        {
            _eyeLerping.AddEye(preview.Value);
            _currentCamera = preview;
        }
        else if (_currentCamera != preview)
        {
            _eyeLerping.RemoveEye(_currentCamera.Value);
            _eyeLerping.AddEye(preview.Value);
            _currentCamera = preview;
        }

        if (_entManager.TryGetComponent<EyeComponent>(_currentCamera, out var eye))
            _cameraView.Eye = eye.Eye ?? _defaultEye;

        UpdateButtons();
    }

    private void CaptureScreenshot()
    {
        if (_disposed || _currentCamera == null)
            return;

        _cameraView.Screenshot(image =>
        {
            if (_disposed)
                return;

            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            CaptureReady?.Invoke(stream.ToArray());
        });
    }

    private void UpdateButtons()
    {
        var hasPreview = _currentCamera != null;
        var hasCharge = _nextState is { CurrentCharges: > 0 };
        var hasLastCapture = _nextState?.HasLastCapture == true;

        _captureButton.Disabled = !hasPreview || !hasCharge;
        _printLastButton.Disabled = !hasLastCapture || !hasCharge;
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_currentCamera != null)
        {
            _eyeLerping.RemoveEye(_currentCamera.Value);
            _currentCamera = null;
        }

        base.Dispose(disposing);
    }
}
