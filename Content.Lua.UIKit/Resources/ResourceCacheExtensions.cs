using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Lua.UIKit.Resources;

public static class ResourceCacheExtensions
{
    public static Font GetFont(this IResourceCache cache, ResPath path, int size) =>
        new VectorFont(cache.GetResource<FontResource>(path), size);

    public static Font GetFont(this IResourceCache cache, string path, int size) =>
        cache.GetFont(new ResPath(path), size);

    public static Font GetFont(this IResourceCache cache, ResPath[] path, int size)
    {
        var fs = new Font[path.Length];
        for (var i = 0; i < path.Length; i++)
            fs[i] = new VectorFont(cache.GetResource<FontResource>(path[i]), size);
        return new StackedFont(fs);
    }

    public static Font GetFont(this IResourceCache cache, string[] path, int size)
    {
        var rp = new ResPath[path.Length];
        for (var i = 0; i < path.Length; i++)
            rp[i] = new ResPath(path[i]);
        return cache.GetFont(rp, size);
    }

    public static Font NotoStack(this IResourceCache resCache, string variation = "Regular", int size = 10, bool display = false)
    {
        var ds = display ? "Display" : "";
        var sv = variation.StartsWith("Bold", StringComparison.Ordinal) ? "Bold" : "Regular";
        return resCache.GetFont(
            new[]
            {
                $"/Fonts/NotoSans{ds}/NotoSans{ds}-{variation}.ttf",
                $"/Fonts/NotoSans/NotoSansSymbols-{sv}.ttf",
                "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
            },
            size);
    }
}
