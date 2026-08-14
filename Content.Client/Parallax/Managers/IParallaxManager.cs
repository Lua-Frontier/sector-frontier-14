using System.Numerics;
using Content.Shared.Parallax;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Parallax.Managers;

public interface IParallaxManager
{
    void LoadDefaultParallax();
    ParallaxPrototype GetPrototype(ProtoId<ParallaxPrototype> id);

    ShaderInstance GetTelescopeBackground();
    ShaderInstance GetTelescopeStarField(int layerIndex);
    ShaderInstance GetCosmicBackground();
    ShaderInstance? GetNamedShader(string? id);
    Texture GetImageTexture(ResPath path);

    Vector2 ParallaxAnchor { get; set; }

    Texture KalisetTexture { get; }
    Texture FireNoise { get; }
    Texture WavyBlotchNoise { get; }
    Texture DendriticNoiseZoomedOut { get; }
    Texture TurbulentNoise { get; }
}
