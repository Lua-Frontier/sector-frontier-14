using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> ParallaxEnabled =
        CVarDef.Create("parallax.enabled", true, CVar.CLIENTONLY);

    public static readonly CVarDef<int> ParallaxQuality =
        CVarDef.Create("parallax.quality", 2, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> ParallaxStarsEnabled =
        CVarDef.Create("parallax.stars", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> ParallaxImagesEnabled =
        CVarDef.Create("parallax.images", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> ParallaxScrollEnabled =
        CVarDef.Create("parallax.scroll", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> ParallaxStarDensity =
        CVarDef.Create("parallax.star_density", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);
}
