using Content.Shared._Arcane.ERP;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client._Arcane.ERP;

public sealed class ArousalOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "ArousalScreenEffect";
    private static readonly ResPath HeartTexturePath = new("/Textures/_Arcane/Interface/heartIcon.png");

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private const float FadeSpeed = 3.5f;
    private const float MinimumVisibleIntensity = 0.005f;
    private const int HeartCount = 10;
    private static readonly HeartData[] Hearts = CreateHeartData();

    private readonly ArousalSystem _arousal;
    private readonly ShaderInstance? _shader;
    private readonly Texture? _heartTexture;
    private float _currentIntensity;
    private float _animationTime;

    public float MotionScale { get; set; } = 1f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ArousalOverlay()
    {
        IoCManager.InjectDependencies(this);

        _arousal = _entityManager.System<ArousalSystem>();

        _shader = _prototypeManager.TryIndex(ShaderProto, out var shader)
            ? shader.InstanceUnique()
            : null;

        _heartTexture = _resourceCache.TryGetResource<TextureResource>(HeartTexturePath, out var texture)
            ? texture.Texture
            : null;

        if (_shader != null && _heartTexture != null)
            return;

        var sawmill = _logManager.GetSawmill("arousal.overlay");

        if (_shader == null)
            sawmill.Warning($"Shader prototype {ShaderProto} is missing; drawing hearts only.");

        if (_heartTexture == null)
            sawmill.Warning($"Texture {HeartTexturePath} is missing; hearts disabled.");
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var targetIntensity = 0f;
        if (_playerManager.LocalEntity is { Valid: true } player &&
            _entityManager.TryGetComponent(player, out ArousalComponent? arousal) &&
            arousal.MaxArousal > 0f)
        {
            var normalizedArousal = Math.Clamp(_arousal.GetArousal(arousal) / arousal.MaxArousal, 0f, 1f);
            targetIntensity = MathF.Pow(normalizedArousal, 0.7f);
        }

        var interpolation = Math.Clamp(args.DeltaSeconds * FadeSpeed, 0f, 1f);
        _currentIntensity = MathHelper.Lerp(_currentIntensity, targetIntensity, interpolation);
        if (MathHelper.CloseTo(_currentIntensity, targetIntensity, 0.001f))
            _currentIntensity = targetIntensity;

        _animationTime += args.DeltaSeconds * MotionScale;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _currentIntensity > MinimumVisibleIntensity;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        if (_shader != null)
        {
            _shader.SetParameter("intensity", _currentIntensity);
            _shader.SetParameter("motionScale", MotionScale);

            handle.UseShader(_shader);
            handle.DrawRect(args.WorldBounds, Color.White);
            handle.UseShader(null);
        }

        DrawHearts(handle, args.WorldBounds);
    }

    private void DrawHearts(DrawingHandleWorld handle, Box2Rotated worldBounds)
    {
        if (_heartTexture == null)
            return;

        var bounds = worldBounds.Box;
        for (var index = 0; index < HeartCount; index++)
        {
            var heart = Hearts[index];
            var progress = Fract(heart.InitialProgress + _animationTime * heart.Speed);

            var presence = SmoothStep((_currentIntensity - heart.Threshold) / 0.12f);
            var spawnFade = SmoothStep(progress / 0.06f);
            var lifetimeFade = MathF.Pow(1f - progress, 1.35f) * spawnFade;
            var alpha = presence * lifetimeFade * _currentIntensity * 0.78f * 0.7f;
            if (alpha <= MinimumVisibleIntensity)
                continue;

            var centerY = MathHelper.Lerp(-0.08f, 1.08f, progress);
            var size = heart.SizeScale * bounds.Height;
            var center = new Vector2(
                MathHelper.Lerp(bounds.Left, bounds.Right, heart.CenterX),
                MathHelper.Lerp(bounds.Bottom, bounds.Top, centerY));
            var box = Box2.CenteredAround(center, new Vector2(size));
            var rotatedBox = new Box2Rotated(box, worldBounds.Rotation, worldBounds.Origin);

            handle.DrawTextureRect(_heartTexture, rotatedBox, Color.White.WithAlpha(alpha));
        }
    }

    private static HeartData[] CreateHeartData()
    {
        var hearts = new HeartData[HeartCount];
        for (var index = 0; index < HeartCount; index++)
        {
            var seed = index + 1f;
            var edgeOffset = 0.035f + Hash(seed * 3.7f) * 0.105f;
            var centerX = index % 2 == 0 ? edgeOffset : 1f - edgeOffset;
            hearts[index] = new HeartData(
                centerX,
                0.035f + Hash(seed * 5.3f) * 0.045f,
                Hash(seed * 8.1f),
                0.22f + Hash(seed * 6.9f) * 0.48f,
                0.032f + Hash(seed * 5.4f) * 0.024f);
        }

        return hearts;
    }

    private static float Hash(float seed)
    {
        return Fract(MathF.Sin(seed * 127.1f) * 43758.5453f);
    }

    private static float Fract(float value)
    {
        return value - MathF.Floor(value);
    }

    private static float SmoothStep(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value * value * (3f - 2f * value);
    }

    private readonly record struct HeartData(
        float CenterX,
        float Speed,
        float InitialProgress,
        float Threshold,
        float SizeScale);
}
