using System.Numerics;
using Content.Client.Parallax.Managers;
using Content.Shared.Parallax;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.ViewVariables;

namespace Content.Client.Parallax;

public sealed class ParallaxControl : Control
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IParallaxManager _parallaxManager = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private string _parallaxPrototype = "FastSpace";

    [ViewVariables(VVAccess.ReadWrite)] public Vector2 Offset { get; set; }
    [ViewVariables(VVAccess.ReadWrite)] public float SpeedX { get; set; }
    [ViewVariables(VVAccess.ReadWrite)] public float SpeedY { get; set; }
    [ViewVariables(VVAccess.ReadWrite)] public float ScaleX { get; set; } = ParallaxShaderHelper.FallbackViewScale;
    [ViewVariables(VVAccess.ReadWrite)] public float ScaleY { get; set; } = ParallaxShaderHelper.FallbackViewScale;

    [ViewVariables(VVAccess.ReadWrite)]
    public string ParallaxPrototype
    {
        get => _parallaxPrototype;
        set => _parallaxPrototype = value;
    }

    public ParallaxControl()
    {
        IoCManager.InjectDependencies(this);
        Offset = new Vector2(_random.Next(0, 1000), _random.Next(0, 1000));
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var currentTime = (float) _timing.RealTime.TotalSeconds;
        var offset = Offset + new Vector2(currentTime * SpeedX, currentTime * SpeedY);
        var bounds = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
        var prototype = _parallaxManager.GetPrototype(new ProtoId<ParallaxPrototype>(_parallaxPrototype));
        var viewScale = Math.Max(MathF.Max(ScaleX, ScaleY), 0.001f);

        ParallaxShaderHelper.Draw(
            handle,
            bounds,
            _parallaxManager,
            _configuration,
            prototype,
            currentTime,
            offset,
            viewScale);
    }
}
