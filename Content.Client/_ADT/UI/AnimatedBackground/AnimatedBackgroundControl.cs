using Content.Shared.ADT;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.ADT.UI.AnimatedBackground;

public sealed class AnimatedBackgroundControl : TextureRect
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private string? _rsiPath;
    private RSI? _rsi;
    private const int States = 1;

    private List<AnimatedLobbyScreenPrototype>? _backgrounds;
    private int _currentBackgroundIndex = -1;
    private IRenderTexture? _buffer;

    private readonly float[] _timer = new float[States];
    private readonly float[][] _frameDelays = new float[States][];
    private readonly int[] _frameCounter = new int[States];
    private readonly Texture[][] _frames = new Texture[States][];

    public AnimatedBackgroundControl()
    {
        IoCManager.InjectDependencies(this);
    }

    private static string NormalizeTexturePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "/Textures/";

        return path.StartsWith("/Textures/") ? path : $"/Textures/{path}";
    }

    public void SetRsiPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            ClearBackground();
            return;
        }

        _rsiPath = NormalizeTexturePath(path);
        LoadFromPath();
    }

    public void SetRSI(RSI? rsi)
    {
        if (rsi == null)
        {
            ClearBackground();
            return;
        }

        _rsi = rsi;
        ApplyRsiStates();
    }

    private void ClearBackground()
    {
        _rsiPath = null;
        _rsi = null;
        Texture = null;

        for (var i = 0; i < States; i++)
        {
            _frames[i] = [];
            _frameDelays[i] = [];
            _frameCounter[i] = 0;
            _timer[i] = 0;
        }
    }

    private void LoadFromPath()
    {
        if (_rsiPath == null)
        {
            ClearBackground();
            return;
        }

        try
        {
            _rsi = _resourceCache.GetResource<RSIResource>(_rsiPath).RSI;
        }
        catch
        {
            _rsiPath = NormalizeTexturePath(_rsiPath);
            _rsi = _resourceCache.GetResource<RSIResource>(_rsiPath).RSI;
        }

        ApplyRsiStates();
    }

    private void ApplyRsiStates()
    {
        if (_rsi == null)
        {
            Texture = null;
            return;
        }

        for (var i = 0; i < States; i++)
        {
            _timer[i] = 0;
            _frameCounter[i] = 0;

            if (!_rsi.TryGetState((i + 1).ToString(), out var state))
            {
                _frames[i] = [];
                _frameDelays[i] = [];
                continue;
            }

            _frames[i] = state.GetFrames(RsiDirection.South);
            _frameDelays[i] = state.GetDelays();

            if (_frames[i].Length > 0)
                Texture = _frames[i][0];
        }
    }

    private List<AnimatedLobbyScreenPrototype> GetAvailableBackgrounds()
    {
        return AnimatedLobbyScreenPrototype.GetAvailable(_prototypeManager, DateTime.Now);
    }

    public void NextBackground()
    {
        CycleBackground(1);
    }

    public void PreviousBackground()
    {
        CycleBackground(-1);
    }

    private void CycleBackground(int delta)
    {
        _backgrounds = GetAvailableBackgrounds();
        if (_backgrounds.Count == 0)
            return;

        _currentBackgroundIndex = _rsiPath == null
            ? -1
            : _backgrounds.FindIndex(p => NormalizeTexturePath(p.Path) == _rsiPath);

        if (_currentBackgroundIndex < 0)
            _currentBackgroundIndex = 0;

        _currentBackgroundIndex = (_currentBackgroundIndex + delta + _backgrounds.Count) % _backgrounds.Count;
        SetRsiPath(_backgrounds[_currentBackgroundIndex].Path);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        for (var i = 0; i < States; i++)
        {
            var delays = _frameDelays[i];
            var frames = _frames[i];
            if (delays == null || frames == null || delays.Length == 0 || frames.Length == 0)
                continue;

            _timer[i] += args.DeltaSeconds;

            var currentFrameIndex = _frameCounter[i];
            if (currentFrameIndex >= delays.Length || currentFrameIndex >= frames.Length)
            {
                _frameCounter[i] = 0;
                currentFrameIndex = 0;
            }

            if (!(_timer[i] >= delays[currentFrameIndex]))
                continue;

            _timer[i] -= delays[currentFrameIndex];
            _frameCounter[i] = (currentFrameIndex + 1) % frames.Length;
            Texture = frames[_frameCounter[i]];
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_buffer is null)
            return;

        handle.DrawTextureRect(_buffer.Texture, PixelSizeBox);
    }

    protected override void Resized()
    {
        base.Resized();
        _buffer?.Dispose();
        _buffer = _clyde.CreateRenderTarget(PixelSize, RenderTargetColorFormat.Rgba8Srgb);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _buffer?.Dispose();
    }

    public void RandomizeBackground()
    {
        var backgroundsProto = GetAvailableBackgrounds();
        if (backgroundsProto.Count == 0)
            return;

        var index = _random.Next(backgroundsProto.Count);
        _backgrounds = backgroundsProto;
        _currentBackgroundIndex = index;
        SetRsiPath(backgroundsProto[index].Path);
    }
}
