using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Lua.Expedition.UI;

public sealed class ExpeditionPlanetPreview : Control
{
    public const int NativeSize = 48;
    public const float DisplayScale = 1.5f;
    [Dependency] private readonly IResourceCache _cache = default!;
    private readonly TextureRect _rect;
    private RSI.State? _state;
    private string? _biomeId;
    private bool _active;
    private int _frame;
    private float _timer;

    public ExpeditionPlanetPreview()
    {
        IoCManager.InjectDependencies(this);
        var size = NativeSize * DisplayScale;
        MinSize = new Vector2(size, size);
        SetSize = new Vector2(size, size);
        HorizontalAlignment = HAlignment.Center;
        _rect = new TextureRect
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Stretch = TextureRect.StretchMode.KeepCentered,
            TextureScale = new Vector2(DisplayScale, DisplayScale),
        };
        AddChild(_rect);
    }

    public void SetBiome(string biomeId)
    {
        if (_biomeId == biomeId) return;
        _biomeId = biomeId;
        if (_active) Reload();
    }

    public void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        if (active) Reload();
        else Unload();
    }

    private void Reload()
    {
        Unload();
        if (string.IsNullOrWhiteSpace(_biomeId)) return;
        ExpeditionPlanetSprites.TryResolve(_biomeId, out var rsiPath, out var stateName);
        var rsi = _cache.GetResource<RSIResource>(rsiPath).RSI;
        if (!rsi.TryGetState(stateName, out var state)) return;
        _state = state;
        _frame = 0;
        _timer = state.GetDelay(0);
        _rect.Texture = state.GetFrame(RsiDirection.South, 0);
        _rect.Visible = true;
    }

    private void Unload()
    {
        _state = null;
        _frame = 0;
        _timer = 0f;
        _rect.Texture = null;
        _rect.Visible = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_active || !VisibleInTree || _state == null || !_state.IsAnimated) return;
        _timer -= args.DeltaSeconds;
        var advanced = false;
        var guard = 0;
        while (_timer <= 0f && guard++ < _state.DelayCount)
        {
            _frame = (_frame + 1) % _state.DelayCount;
            var delay = _state.GetDelay(_frame);
            if (delay <= 0f) delay = 0.01f;
            _timer += delay;
            advanced = true;
        }
        if (advanced) _rect.Texture = _state.GetFrame(RsiDirection.South, _frame);
    }
}
