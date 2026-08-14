#nullable enable
using System.Numerics;
using Content.Client.Parallax.Managers;
using Content.Shared.Parallax;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests;

public sealed class DummyParallaxManager : IParallaxManager
{
    public Vector2 ParallaxAnchor { get; set; }
    public Texture KalisetTexture => Texture.White;
    public Texture FireNoise => Texture.White;
    public Texture WavyBlotchNoise => Texture.White;
    public Texture DendriticNoiseZoomedOut => Texture.White;
    public Texture TurbulentNoise => Texture.White;

    public void LoadDefaultParallax()
    {
    }

    public ParallaxPrototype GetPrototype(ProtoId<ParallaxPrototype> id)
    {
        throw new NotSupportedException();
    }

    public ShaderInstance GetTelescopeBackground() => throw new NotSupportedException();
    public ShaderInstance GetTelescopeStarField(int layerIndex) => throw new NotSupportedException();
    public ShaderInstance GetCosmicBackground() => throw new NotSupportedException();
    public ShaderInstance? GetNamedShader(string? id) => null;
    public Texture GetImageTexture(ResPath path) => Texture.White;
}
