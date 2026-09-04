using System.Numerics;
using Content.Shared._RMC14.Explosion;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Explosion;

public sealed class RMCExplosionShockWaveOverlay : Overlay, IEntityEventSubscriber
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private SharedTransformSystem? _xformSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public const int MaxCount = 10;

    public RMCExplosionShockWaveOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index<ShaderPrototype>("RMCShockWave").Instance().Duplicate();
    }

    private readonly Vector2[] _positions = new Vector2[MaxCount];
    private readonly float[] _falloffPower = new float[MaxCount];
    private readonly float[] _sharpness = new float[MaxCount];
    private readonly float[] _width = new float[MaxCount];
    private readonly float[] _currentTime = new float[MaxCount];
    private int _count;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null || _xformSystem is null && !_entMan.TrySystem(out _xformSystem))
            return false;

        var query = _entMan.EntityQueryEnumerator<RMCExplosionShockWaveComponent, TransformComponent>();

        _count = 0;

        while (query.MoveNext(out var uid, out var distortion, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var mapPos = _xformSystem.GetWorldPosition(uid);

            var tempCoords = args.Viewport.WorldToLocal(mapPos);

            tempCoords.Y = 1 - (tempCoords.Y / args.Viewport.Size.Y);
            tempCoords.X /= args.Viewport.Size.X;

            _positions[_count] = tempCoords;
            _falloffPower[_count] = distortion.FalloffPower;
            _sharpness[_count] = distortion.Sharpness;
            _width[_count] = distortion.Width;
            _currentTime[_count] = (float)(_timing.CurTime - distortion.StartTime).TotalSeconds;
            _count++;

            if (_count == MaxCount)
                break;
        }

        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        _shader?.SetParameter("renderScale", args.Viewport.RenderScale * args.Viewport.Eye.Scale);
        _shader?.SetParameter("count", _count);
        _shader?.SetParameter("position", _positions);
        _shader?.SetParameter("falloffPower", _falloffPower);
        _shader?.SetParameter("sharpness", _sharpness);
        _shader?.SetParameter("width", _width);
        _shader?.SetParameter("currentTime", _currentTime);
        _shader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}
