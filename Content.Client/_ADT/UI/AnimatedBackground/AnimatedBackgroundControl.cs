using System.Linq;
using Content.Shared.ADT;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.ADT.UI.AnimatedBackground;

public sealed class AnimatedBackgroundControl : TextureRect
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private string _rsiPath = "/Textures/_Lua/LobbyScreens/backgrounds/cube.rsi";
    public RSI? _RSI;
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

        InitializeStates();
    }

    private void EnsureBackgroundsLoaded()
    {
        if (_backgrounds != null) return;
        _backgrounds = _prototypeManager.EnumeratePrototypes<AnimatedLobbyScreenPrototype>().ToList();
        if (_backgrounds.Count == 0) return;
        _currentBackgroundIndex = _backgrounds.FindIndex(p => NormalizeTexturePath(p.Path) == _rsiPath);
        if (_currentBackgroundIndex < 0) _currentBackgroundIndex = 0;
    }

    private static string NormalizeTexturePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "/Textures/";
        return path.StartsWith("/Textures/") ? path : $"/Textures/{path}";
    }

    private void InitializeStates()
    {
        try
        {
            _RSI = _resourceCache.GetResource<RSIResource>(_rsiPath).RSI;
        }
        catch
        {
            var normalized = NormalizeTexturePath(_rsiPath);
            _RSI = _resourceCache.GetResource<RSIResource>(normalized).RSI;
            _rsiPath = normalized;
        }

        for (var i = 0; i < States; i++)
        {
            if (!_RSI.TryGetState((i + 1).ToString(), out var state))
                continue;

            _frames[i] = state.GetFrames(RsiDirection.South);
            _frameDelays[i] = state.GetDelays();
            _frameCounter[i] = 0;
        }
    }

    public void SetRSI(RSI? rsi)
    {
        _RSI = rsi;
        InitializeStates();
    }

    public void NextBackground()
    {
        EnsureBackgroundsLoaded();
        if (_backgrounds == null || _backgrounds.Count == 0) return;
        _currentBackgroundIndex = (_currentBackgroundIndex + 1) % _backgrounds.Count;
        _rsiPath = NormalizeTexturePath(_backgrounds[_currentBackgroundIndex].Path);
        InitializeStates();
    }

    public void PreviousBackground()
    {
        EnsureBackgroundsLoaded();
        if (_backgrounds == null || _backgrounds.Count == 0) return;
        _currentBackgroundIndex--;
        if (_currentBackgroundIndex < 0)  _currentBackgroundIndex = _backgrounds.Count - 1;
        _rsiPath = NormalizeTexturePath(_backgrounds[_currentBackgroundIndex].Path);
        InitializeStates();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        for (var i = 0; i < _frames.Length; i++)
        {
            var delays = _frameDelays[i];
            if (delays.Length == 0)
                continue;

            _timer[i] += args.DeltaSeconds;

            var currentFrameIndex = _frameCounter[i];

            if (!(_timer[i] >= delays[currentFrameIndex]))
                continue;

            _timer[i] -= delays[currentFrameIndex];
            _frameCounter[i] = (currentFrameIndex + 1) % _frames[i].Length;
            Texture = _frames[i][_frameCounter[i]];
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
        var backgroundsProto = _prototypeManager.EnumeratePrototypes<AnimatedLobbyScreenPrototype>().ToList();
        var random = new Random();
        var index = random.Next(backgroundsProto.Count);
        _rsiPath = NormalizeTexturePath(backgroundsProto[index].Path);
        InitializeStates();
    }
}
