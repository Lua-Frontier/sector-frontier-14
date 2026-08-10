using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared.Parallax;

public enum ParallaxStyle : byte
{
    Telescope = 0,
    Cosmic = 1,
}

[DataDefinition]
public sealed partial class ParallaxStarLayerData
{
    [DataField]
    public float Slowness { get; private set; } = 0.999f;

    [DataField]
    public float WorldScale { get; private set; } = 2f;

    [DataField]
    public float Cutoff { get; private set; } = 0.9f;

    [DataField]
    public float Power { get; private set; } = 12f;

    [DataField]
    public float Intensity { get; private set; } = 1f;

    [DataField]
    public float SizeMinPx { get; private set; } = 1f;

    [DataField]
    public float SizeMaxPx { get; private set; } = 2f;

    [DataField]
    public Vector2 SeedBias { get; private set; }
}

[DataDefinition]
public sealed partial class ParallaxImageLayerData
{
    [DataField(required: true)]
    public ResPath Path { get; private set; }

    [DataField]
    public float Slowness { get; private set; } = 0.5f;

    [DataField]
    public Vector2 Scale { get; private set; } = Vector2.One;

    [DataField]
    public bool Tiled { get; private set; } = true;

    [DataField]
    public Vector2 WorldHomePosition { get; private set; }

    [DataField]
    public Vector2 WorldAdjustPosition { get; private set; }

    [DataField]
    public Vector2 ControlHomePosition { get; private set; }

    [DataField]
    public Vector2 Scrolling { get; private set; }

    [DataField]
    public string? Shader { get; private set; } = "unshaded";
}

[DataDefinition]
public sealed partial class ParallaxCosmicSettings
{
    [DataField]
    public float Zoom { get; private set; } = 0.12f;

    [DataField]
    public float Brightness { get; private set; } = 1f;

    [DataField]
    public float DefaultScroll { get; private set; } = 0.0015f;

    [DataField]
    public float FrontStarColorMul { get; private set; } = 0.5f;

    [DataField]
    public float BackStarColorMul { get; private set; } = 0.4f;

    [DataField]
    public Vector3 ColorChangeInfluence1 { get; private set; } = new(-2.1f, 0.5f, 2.15f);

    [DataField]
    public Vector3 ColorChangeInfluence2 { get; private set; } = new(2.4f, -0.9f, 5.8f);

    [DataField]
    public float ColorChangeStrength1 { get; private set; } = 0.6f;

    [DataField]
    public float ColorChangeStrength2 { get; private set; } = 0.81f;
}

[Prototype]
public sealed partial class ParallaxPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<ParallaxPrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField]
    [NeverPushInheritance]
    public bool Abstract { get; private set; }

    [DataField]
    public ParallaxStyle Style { get; private set; } = ParallaxStyle.Telescope;

    [DataField]
    public float Seed { get; private set; }

    [DataField]
    public Vector2 ScrollSpeed { get; private set; }

    [DataField]
    public float StarDensity { get; private set; } = 1f;

    [DataField]
    public Color BaseColor { get; private set; } = new(7 / 255f, 7 / 255f, 15 / 255f);

    [DataField]
    public Color NebulaColor { get; private set; } = new(0f, 1f, 0.68f);

    [DataField]
    public Color AccentColor { get; private set; } = new(1f, 0.15f, 0.5f);

    [DataField]
    public Color HorizonColor { get; private set; } = new(0.3f, 0f, 1f);

    [DataField]
    public int LayerCount { get; private set; } = 16;

    [DataField]
    public int LayerCountLQ { get; private set; } = 10;

    [DataField]
    public float ViewScale { get; private set; } = 2f;

    [DataField]
    public float UiZoom { get; private set; } = 16f;

    [DataField]
    public float TwinkleSpeed { get; private set; } = 1.6f;

    [DataField]
    public float NebulaExponent { get; private set; } = 1.5f;

    [DataField]
    public float NebulaIntensity { get; private set; }

    [DataField]
    public float BgSlowness { get; private set; } = 0.9992f;

    [DataField]
    public float BgWorldScale { get; private set; } = 0.022f;

    [DataField]
    public float BgNebulaMul { get; private set; }

    [DataField]
    public float HorizonColorMul { get; private set; } = 0.35f;

    [DataField]
    public float BaseColorMul { get; private set; } = 0.60f;

    [DataField]
    public List<ParallaxStarLayerData> StarLayers { get; private set; } = new();

    [DataField]
    public List<int> LowQualityStarLayers { get; private set; } = new() { 0, 2, 4, 6 };

    [DataField]
    public List<ParallaxImageLayerData> ImageLayers { get; private set; } = new();

    [DataField]
    public ParallaxCosmicSettings Cosmic { get; private set; } = new();
}
