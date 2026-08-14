using System.Numerics;
using Content.Shared.Parallax;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Parallax.Managers;

public sealed class ParallaxManager : IParallaxManager
{
    private const int KalisetSize = 1024;
    private const int KalisetIterations = 14;
    private const float KalisetJulia = 0.584f;

    private static readonly ProtoId<ShaderPrototype> TelescopeBg = "WotGTelescopeBackground";
    private static readonly ProtoId<ShaderPrototype> TelescopeStars = "WotGTelescopeStarField";
    private static readonly ProtoId<ShaderPrototype> CosmicBg = "WotGCosmicBackground";

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IResourceCache _resources = default!;

    private readonly Dictionary<string, ShaderInstance> _shaders = new();
    private readonly Dictionary<ResPath, Texture> _images = new();
    private ShaderInstance[]? _starLayerShaders;
    private Texture? _kaliset;
    private Texture? _fireNoise;
    private Texture? _wavyBlotch;
    private Texture? _dendritic;
    private Texture? _turbulent;

    public Vector2 ParallaxAnchor { get; set; }

    public Texture KalisetTexture => _kaliset ??= GenerateKalisetTexture();
    public Texture FireNoise => _fireNoise ??= LoadNoise("FireNoiseA.png");
    public Texture WavyBlotchNoise => _wavyBlotch ??= LoadNoise("WavyBlotchNoise.png");
    public Texture DendriticNoiseZoomedOut => _dendritic ??= LoadNoise("DendriticNoiseZoomedOut.png");
    public Texture TurbulentNoise => _turbulent ??= LoadNoise("TurbulentNoise.png");

    public void LoadDefaultParallax()
    {
        _ = GetPrototype("Default");
        _ = KalisetTexture;
        _ = FireNoise;
        _ = WavyBlotchNoise;
        _ = DendriticNoiseZoomedOut;
        _ = TurbulentNoise;
        _ = GetTelescopeBackground();
        var defaultLayers = GetPrototype("Default").StarLayers.Count;
        for (var i = 0; i < Math.Max(defaultLayers, 1); i++)
            _ = GetTelescopeStarField(i);
        _ = GetCosmicBackground();
    }

    public ParallaxPrototype GetPrototype(ProtoId<ParallaxPrototype> id) => _prototypes.Index(id);

    public ShaderInstance GetTelescopeBackground() => GetShader(TelescopeBg);

    public ShaderInstance GetTelescopeStarField(int layerIndex)
    {
        layerIndex = Math.Max(layerIndex, 0);
        _starLayerShaders ??= new ShaderInstance[Math.Max(layerIndex + 1, 8)];
        if (layerIndex >= _starLayerShaders.Length)
            Array.Resize(ref _starLayerShaders, layerIndex + 1);

        return _starLayerShaders[layerIndex] ??= _prototypes.Index(TelescopeStars).InstanceUnique();
    }

    public ShaderInstance GetCosmicBackground() => GetShader(CosmicBg);

    public ShaderInstance? GetNamedShader(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        return GetShader(new ProtoId<ShaderPrototype>(id));
    }

    public Texture GetImageTexture(ResPath path)
    {
        if (_images.TryGetValue(path, out var cached))
            return cached;

        var tex = _resources.GetResource<TextureResource>(path).Texture;
        _images[path] = tex;
        return tex;
    }

    private ShaderInstance GetShader(ProtoId<ShaderPrototype> id)
    {
        var key = id.Id;
        if (_shaders.TryGetValue(key, out var shader))
            return shader;

        shader = _prototypes.Index(id).InstanceUnique();
        _shaders.Add(key, shader);
        return shader;
    }

    private Texture LoadNoise(string fileName)
    {
        return _resources.GetResource<TextureResource>($"/Textures/_Lua/Effects/WotG/Noise/{fileName}").Texture;
    }

    private Texture GenerateKalisetTexture()
    {
        var image = new Image<Rgba32>(KalisetSize, KalisetSize);
        var julia = new Vector2(KalisetJulia, KalisetJulia);
        var invSize = 1f / KalisetSize;

        for (var y = 0; y < KalisetSize; y++)
        {
            var py = y * invSize - 0.5f;
            for (var x = 0; x < KalisetSize; x++)
            {
                var p = new Vector2(x * invSize - 0.5f, py);
                var previousDistance = 0f;
                var totalChange = 0f;

                for (var iteration = 0; iteration < KalisetIterations; iteration++)
                {
                    var lengthSquared = MathF.Max(Vector2.Dot(p, p), 0.000001f);
                    p = new Vector2(MathF.Abs(p.X), MathF.Abs(p.Y)) / lengthSquared - julia;
                    var distance = p.Length();
                    totalChange += MathF.Abs(distance - previousDistance);
                    previousDistance = distance;
                }

                if (float.IsNaN(totalChange) || float.IsInfinity(totalChange) || totalChange > 1000f)
                    totalChange = 1000f;

                var encoded = (byte) (totalChange * (255f / 1000f));
                image[x, y] = new Rgba32(encoded, encoded, encoded, 255);
            }
        }

        return LoadGenerated(image, "wotg-kaliset", filter: true, wrap: true);
    }

    private Texture LoadGenerated(Image<Rgba32> image, string name, bool filter, bool wrap)
    {
        return _clyde.LoadTextureFromImage(image,
            name: name,
            loadParams: new TextureLoadParameters
            {
                Srgb = false,
                Preload = false,
                SampleParameters = new TextureSampleParameters
                {
                    Filter = filter,
                    WrapMode = wrap ? TextureWrapMode.Repeat : TextureWrapMode.None,
                },
            });
    }
}
