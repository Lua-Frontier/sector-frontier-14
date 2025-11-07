using Content.Server.Popups;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Server._Lua.Horror;

public sealed partial class LumenAversionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly MapSystem _maps = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly PopupSystem _popups = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(2);
    private TimeSpan _nextTick = TimeSpan.Zero;
    private const float MaxIntensity = 10f;

    private float ComputeLightExposure(EntityUid target, TransformComponent destXform)
    {
        var mapCoords = _xform.GetMapCoordinates(destXform);
        var worldPos = _xform.GetWorldPosition(destXform);
        var lights = _lookup.GetEntitiesInRange<PointLightComponent>(mapCoords, 20f);
        var intensity = 0f;
        foreach (var light in lights)
        {
            if (!light.Comp.Enabled) continue;
            var srcXform = Transform(light);
            var srcPos = _xform.GetWorldPosition(srcXform);
            var toTarget = worldPos - srcPos;
            var distance = toTarget.Length();
            if (distance > light.Comp.Radius) continue;
            if (IsDirectional(light, srcXform))
            {
                var forward = AngleToVector(_xform.GetWorldRotation(srcXform));
                var dir = Vector2.Normalize(toTarget);
                if (Vector2.Dot(forward, dir) < 0.70710677f) continue;
            }
            if (IsOccluded(srcXform, destXform, srcPos, worldPos)) continue;
            var sample = MathF.Max(0f, light.Comp.Radius - distance);
            sample *= light.Comp.Energy;
            if (sample > intensity) intensity = sample;
        }
        if (intensity > MaxIntensity) intensity = MaxIntensity;
        if (intensity < 0f) intensity = 0f;
        return intensity;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextTick) return;
        _nextTick = now + TickInterval;
        var query = EntityQueryEnumerator<LumenAversionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Enabled) continue;
            var exposure = ComputeLightExposure(uid, xform);
            if (exposure > 1.5f)
            {
                var scaled = comp.LightDamage * exposure;
                _damage.TryChangeDamage(uid, scaled, true, false);
                _popups.PopupEntity("Источник света обжигает вас", uid, uid);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/Guns/Hits/energy_meat1.ogg"), uid, AudioParams.Default.WithVolume(-10f).WithVariation(0.25f));
            }
            else if (exposure < 1.0f)
            { _damage.TryChangeDamage(uid, comp.DarkRegen, true, false); }
        }
    }

    private static bool IsDirectional(Entity<PointLightComponent> light, TransformComponent srcXform)
    {
        var mask = light.Comp.MaskPath;
        return mask != null && mask.EndsWith("cone.png");
    }

    private static Vector2 AngleToVector(Angle ang)
    { var rot = ang.ToVec(); return new Vector2(rot.X, rot.Y); }

    private bool IsOccluded(TransformComponent srcXform, TransformComponent dstXform, Vector2 srcWorld, Vector2 dstWorld)
    {
        var aabb = Box2.FromTwoPoints(srcWorld, dstWorld);
        var grids = new List<Entity<MapGridComponent>>();
        _mapMan.FindGridsIntersecting(srcXform.MapID, aabb, ref grids, true);
        foreach (var grid in grids)
        {
            var gridXform = Transform(grid);
            var srcLocal = srcXform.ParentUid == grid.Owner ? srcXform.LocalPosition : Vector2.Transform(srcWorld, gridXform.InvLocalMatrix);
            var dstLocal = dstXform.ParentUid == grid.Owner ? dstXform.LocalPosition : Vector2.Transform(dstWorld, gridXform.InvLocalMatrix);
            var sx = (int)MathF.Floor(srcLocal.X / grid.Comp.TileSize);
            var sy = (int)MathF.Floor(srcLocal.Y / grid.Comp.TileSize);
            var dx = (int)MathF.Floor(dstLocal.X / grid.Comp.TileSize);
            var dy = (int)MathF.Floor(dstLocal.Y / grid.Comp.TileSize);
            var line = new GridLineEnumerator(new Vector2i(sx, sy), new Vector2i(dx, dy));
            while (line.MoveNext())
            {
                foreach (var ent in _maps.GetAnchoredEntities(grid, grid.Comp, line.Current))
                { if (TryComp<OccluderComponent>(ent, out var occ) && occ.Enabled) return true; }
            }
        }
        return false;
    }
}


