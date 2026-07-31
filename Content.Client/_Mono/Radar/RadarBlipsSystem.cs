using System.Numerics;
using Content.Shared._Mono.Radar;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Client._Mono.Radar;

public sealed partial class RadarBlipsSystem : EntitySystem
{
    private const double BlipStaleSeconds = 3.0;
    private static readonly List<RadarBlipNetData> EmptyRawBlipList = new();
    private static readonly List<MissileVectorNetData> EmptyMissileList = new();
    private static readonly List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> EmptyHitscanList = new();
    private TimeSpan _lastRequestTime = TimeSpan.Zero;
    private static readonly TimeSpan RequestThrottle = TimeSpan.FromMilliseconds(250);

    // Maximum distance for blips to be considered visible
    private const float MaxBlipRenderDistance = 1000f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private TimeSpan _lastUpdatedTime;
    private List<RadarBlipNetData> _blips = new();
    private List<MissileVectorNetData> _missiles = new();
    private List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> _hitscans = new();
    private Vector2 _radarWorldPosition;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GiveBlipsEvent>(HandleReceiveBlips);
        SubscribeNetworkEvent<BlipRemovalEvent>(RemoveBlip);
    }

    private void HandleReceiveBlips(GiveBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (ev?.Blips == null)
        {
            _blips = EmptyRawBlipList;
        }
        else
        {
            _blips = ev.Blips;
        }

        if (ev?.Missiles == null)
        {
            _missiles = EmptyMissileList;
        }
        else
        {
            _missiles = ev.Missiles;
        }

        if (ev?.HitscanLines == null)
        {
            _hitscans = EmptyHitscanList;
        }
        else
        {
            _hitscans = ev.HitscanLines;
        }

        _lastUpdatedTime = _timing.CurTime;
    }

    private void RemoveBlip(BlipRemovalEvent args)
    {
        var blipid = _blips.FirstOrDefault(x => x.Uid == args.NetBlipUid);
        _blips.Remove(blipid);
    }

    public void RequestBlips(EntityUid console)
    {
        // Only request if we have a valid console
        if (!Exists(console))
            return;

        // Add request throttling to avoid network spam
        if (_timing.CurTime - _lastRequestTime < RequestThrottle)
            return;

        _lastRequestTime = _timing.CurTime;

        // Cache the radar position for distance culling
        if (TryComp<TransformComponent>(console, out var xform))
        {
            _radarWorldPosition = _xform.GetWorldPosition(console);
        }

        var netConsole = GetNetEntity(console);
        var ev = new RequestBlipsEvent(netConsole);
        RaiseNetworkEvent(ev);
    }

    /// <summary>
    /// Gets the current blips as world positions with their scale, color and shape.
    /// </summary>
    public List<(NetEntity NetUid, EntityCoordinates Position, float Scale, Color Color, RadarBlipShape Shape, bool SonarEcho, BlipConfig? GridConfig)> GetCurrentBlips()
    {
        // If it's been more than the stale threshold since our last update,
        // the data is considered stale - return an empty list
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return new();

        var result = new List<(NetEntity, EntityCoordinates, float, Color, RadarBlipShape, bool, BlipConfig?)>(_blips.Count);

        foreach (var blip in _blips)
        {
            var coord = GetCoordinates(blip.Position);

            if (!coord.IsValid(EntityManager))
                continue;

            var predictedPos = new EntityCoordinates(coord.EntityId, coord.Position + blip.Vel * (float)(_timing.CurTime - _lastUpdatedTime).TotalSeconds);

            // Distance culling for world position blips
            if (Vector2.DistanceSquared(_xform.ToMapCoordinates(predictedPos).Position, _radarWorldPosition) > MaxBlipRenderDistance * MaxBlipRenderDistance)
                continue;

            result.Add((blip.Uid, predictedPos, blip.Scale, blip.Color, blip.Shape, blip.SonarEcho, blip.GridConfig));
        }

        return result;
    }

    /// <summary>
    /// Gets seeking / SACLOS missile direction and FOV arcs in world coordinates.
    /// </summary>
    public List<(Vector2 Start, Vector2 End, Color Color)> GetMissileLines()
    {
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return new();

        var result = new List<(Vector2, Vector2, Color)>(_missiles.Count * 3);
        var dt = (float)(_timing.CurTime - _lastUpdatedTime).TotalSeconds;
        var color = Color.FromHex("#00AACC");
        var colorArcs = Color.FromHex("#FF0040");

        foreach (var missile in _missiles)
        {
            var tiedBlip = _blips.FirstOrDefault(x => x.Uid == missile.Uid);
            if (tiedBlip.Uid == default)
                continue;

            var coord = GetCoordinates(tiedBlip.Position);
            if (!coord.IsValid(EntityManager))
                continue;

            var predictedPos = new EntityCoordinates(coord.EntityId, coord.Position + tiedBlip.Vel * dt);
            var start = _xform.ToMapCoordinates(predictedPos).Position;

            if (Vector2.DistanceSquared(start, _radarWorldPosition) > MaxBlipRenderDistance * MaxBlipRenderDistance)
                continue;

            // Match Monolith: Cos/Sin of (Rotation - 90°) for facing.
            var facing = tiedBlip.Rotation.Theta + Math.PI * -0.5;
            var end = start + new Vector2(
                missile.Range * 0.5f * (float)Math.Cos(facing),
                missile.Range * 0.5f * (float)Math.Sin(facing));

            result.Add((start, end, color));

            if (missile.ScanArc > Angle.Zero)
            {
                var halfArc = missile.ScanArc.Theta * 0.5;
                var left = start + new Vector2(
                    missile.Range * (float)Math.Cos(facing - halfArc),
                    missile.Range * (float)Math.Sin(facing - halfArc));
                var right = start + new Vector2(
                    missile.Range * (float)Math.Cos(facing + halfArc),
                    missile.Range * (float)Math.Sin(facing + halfArc));
                result.Add((start, left, colorArcs));
                result.Add((start, right, colorArcs));
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the hitscan lines to be rendered on the radar
    /// </summary>
    public List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> GetHitscanLines()
    {
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return new List<(Vector2, Vector2, float, Color)>();

        var result = new List<(Vector2 Start, Vector2 End, float Thickness, Color Color)>(_hitscans.Count);

        foreach (var hitscan in _hitscans)
        {
            var worldStart = hitscan.Start;
            var worldEnd = hitscan.End;

            // Distance culling - check if either end of the line is in range
            var startDist = Vector2.DistanceSquared(worldStart, _radarWorldPosition);
            var endDist = Vector2.DistanceSquared(worldEnd, _radarWorldPosition);

            if (startDist > MaxBlipRenderDistance * MaxBlipRenderDistance &&
                endDist > MaxBlipRenderDistance * MaxBlipRenderDistance)
                continue;

            result.Add((worldStart, worldEnd, hitscan.Thickness, hitscan.Color));
        }

        return result;
    }
}
