using System.Numerics;
using Content.Client.Parallax.Managers;
using Content.Shared.Parallax;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Parallax;

public sealed class ParallaxSystem : SharedParallaxSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IParallaxManager _parallax = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private static readonly ProtoId<ParallaxPrototype> Fallback = "Default";

    public const int ParallaxZIndex = 0;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new ParallaxOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ParallaxOverlay>();
    }

    public ParallaxPrototype GetParallaxPrototype(MapId mapId)
    {
        return _parallax.GetPrototype(GetParallax(_map.GetMapOrInvalid(mapId)));
    }

    public ProtoId<ParallaxPrototype> GetParallax(MapId mapId)
    {
        return GetParallax(_map.GetMapOrInvalid(mapId));
    }

    public ProtoId<ParallaxPrototype> GetParallax(EntityUid mapUid)
    {
        return TryComp<ParallaxComponent>(mapUid, out var parallax) ? parallax.Parallax : Fallback;
    }

    public void DrawParallax(
        DrawingHandleWorld worldHandle,
        Box2 worldAABB,
        Texture sprite,
        TimeSpan curTime,
        Vector2 position,
        Vector2 scrolling,
        float scale = 1f,
        float slowness = 0f,
        Color? modulate = null)
    {
        var size = sprite.Size / (float) EyeManager.PixelsPerMeter * scale;
        var scrolled = scrolling * (float) curTime.TotalSeconds;
        var originBL = position * slowness + scrolled;
        originBL -= size / 2;

        var flooredBL = worldAABB.BottomLeft - originBL;
        flooredBL = (flooredBL / size).Floored() * size;
        flooredBL += originBL;

        for (var x = flooredBL.X; x < worldAABB.Right; x += size.X)
        {
            for (var y = flooredBL.Y; y < worldAABB.Top; y += size.Y)
            {
                var box = Box2.FromDimensions(new Vector2(x, y), size);
                worldHandle.DrawTextureRect(sprite, box, modulate);
            }
        }
    }
}
