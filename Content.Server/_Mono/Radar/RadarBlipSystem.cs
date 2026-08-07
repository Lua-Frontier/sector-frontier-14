using Content.Server._Mono.Projectiles.TargetGuided;
using Content.Server._Mono.Projectiles.TargetSeeking;
using Content.Server._Lua.SpaceHazards;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared._Mono.Radar;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Mono.Radar;

public sealed partial class RadarBlipSystem : EntitySystem
{
    private static readonly TimeSpan MobBlipInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan VeilCacheTtl = TimeSpan.FromSeconds(1);

    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpaceHazardActivitySystem _hazardActivity = default!;

    private TimeSpan _nextMobBlipCheck;
    private readonly Dictionary<MapId, (TimeSpan BuiltAt, List<(AmbientSpaceFieldComponent Field, Vector2 Pos)> Fields)> _veilCache = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);
        SubscribeLocalEvent<RadarBlipComponent, ComponentShutdown>(OnBlipShutdown);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextMobBlipCheck)
            return;

        _nextMobBlipCheck = now + MobBlipInterval;

        var mobQuery = EntityQueryEnumerator<MobStateComponent, HumanoidAppearanceComponent>();
        while (mobQuery.MoveNext(out var mobUid, out var mobState, out _))
        {
            var isDead = mobState.CurrentState == MobState.Dead;
            if (isDead)
            {
                if (!HasComp<RadarBlipComponent>(mobUid))
                {
                    var rb = EnsureComp<RadarBlipComponent>(mobUid);
                    rb.VisibleFromOtherGrids = true;
                    rb.RequireNoGrid = false;
                    rb.RadarColor = Color.Red;
                    rb.Scale = 0f;
                    rb.Enabled = true;
                    rb.MaxDistance = 512f;
                    var icon = EnsureComp<RadarBlipIconComponent>(mobUid);
                    icon.Icon = new Robust.Shared.Utility.ResPath("/Textures/_Lua/Interface/Radar/dead_cross.png");
                    icon.Scale = 0.6f;
                }
            }
            else
            {
                RemComp<RadarBlipComponent>(mobUid);
                RemComp<RadarBlipIconComponent>(mobUid);
            }
        }
    }

    private void OnBlipsRequested(RequestBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(ev.Radar, out var radarUid))
            return;

        if (!TryComp<RadarConsoleComponent>(radarUid, out var radar))
            return;


        var blips = AssembleBlipsReport((EntityUid)radarUid, radar);
        var missiles = AssembleMissileReport((EntityUid)radarUid, radar);
        var hitscans = AssembleHitscanReport((EntityUid)radarUid, radar);

        var giveEv = new GiveBlipsEvent(blips, missiles, hitscans);
        RaiseNetworkEvent(giveEv, args.SenderSession);

        blips.Clear();
        missiles.Clear();
        hitscans.Clear();
    }

    private void OnBlipShutdown(EntityUid blipUid, RadarBlipComponent component, ComponentShutdown args)
    {
        var netBlipUid = GetNetEntity(blipUid);
        var removalEv = new BlipRemovalEvent(netBlipUid);
        RaiseNetworkEvent(removalEv);
    }

    private List<RadarBlipNetData> AssembleBlipsReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var blips = new List<RadarBlipNetData>();

        if (Resolve(uid, ref component))
        {
            var radarXform = Transform(uid);
            var radarPosition = _xform.GetWorldPosition(uid);
            var radarGrid = _xform.GetGrid(uid);
            var radarMapId = radarXform.MapID;
            var veilFields = CollectActiveVeilFields(radarMapId);

            var blipQuery = EntityQueryEnumerator<RadarBlipComponent, TransformComponent>();

            while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform))
            {
                if (!blip.Enabled)
                    continue;

                // This prevents blips from showing on radars that are on different maps
                if (blipXform.MapID != radarMapId)
                    continue;

                if (IsHiddenByNebulaVeil(blipUid, blipXform, veilFields))
                    continue;

                var netBlipUid = GetNetEntity(blipUid);

                var blipGrid = blipXform.GridUid;

                var blipVelocity = Vector2.Zero;
                if (TryComp<PhysicsComponent>(blipUid, out var blipPhysics))
                    blipVelocity = _physics.GetMapLinearVelocity(blipUid, blipPhysics, blipXform);

                var distance = (_xform.GetWorldPosition(blipXform) - radarPosition).Length();
                float maxDistance = blip.MaxDistance;
                var radarMax = component?.MaxRange ?? SharedRadarConsoleSystem.DefaultMaxRange;
                var allowedDistance = Math.Min(maxDistance, radarMax);
                if (distance > allowedDistance) continue;
                if ((blip.RequireNoGrid && blipGrid != null) || (!blip.VisibleFromOtherGrids && blipGrid != radarGrid)) continue;

                // due to PVS being a thing, things will break if we try to parent to not the map or a grid
                var coord = blipXform.Coordinates;
                if (blipXform.ParentUid != blipXform.MapUid && blipXform.ParentUid != blipGrid)
                    coord = _xform.WithEntityId(coord, blipGrid ?? blipXform.MapUid!.Value);
                // we're parented to either the map or a grid and this is relative velocity so account for grid movement
                if (blipGrid != null && TryComp<PhysicsComponent>(blipGrid.Value, out var gridBody)) // prevent Resolve log spam
                    blipVelocity -= _physics.GetLinearVelocity(blipGrid.Value, coord.Position, gridBody);

                var scale = blip.Scale;
                var color = blip.RadarColor;
                var shape = blip.Shape;
                BlipConfig? gridConfig = null;

                // On-grid override (Monolith GridConfig): footprint markers for reactors/turbines/etc.
                if (blipGrid != null && blip.GridConfig is { } cfg)
                {
                    gridConfig = cfg;
                    color = cfg.Color;
                    shape = cfg.Shape;
                    scale = cfg.GetScale();
                }

                var sonarEcho = HasComp<RadarSonarEchoComponent>(blipUid);
                var rotation = _xform.GetWorldRotation(blipXform);
                blips.Add(new RadarBlipNetData(netBlipUid, GetNetCoordinates(coord), blipVelocity, scale, color, shape, sonarEcho, gridConfig, rotation));
            }
        }

        return blips;
    }

    /// <summary>
    /// Assembles seeking / SACLOS missile arc overlays (Monolith).
    /// </summary>
    private List<MissileVectorNetData> AssembleMissileReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var missiles = new List<MissileVectorNetData>();

        if (!Resolve(uid, ref component))
            return missiles;

        var radarPosition = _xform.GetWorldPosition(uid);
        var radarMapId = Transform(uid).MapID;
        var radarMax = component.MaxRange;
        var veilFields = CollectActiveVeilFields(radarMapId);

        var missileQuery = EntityQueryEnumerator<TargetSeekingComponent, RadarBlipComponent, TransformComponent>();
        while (missileQuery.MoveNext(out var missile, out var seeker, out var missileBlip, out var missileXform))
        {
            if (!missileBlip.Enabled || !seeker.ArcLines)
                continue;

            if (missileXform.MapID != radarMapId)
                continue;

            if (IsHiddenByNebulaVeil(missile, missileXform, veilFields))
                continue;

            if ((_xform.GetWorldPosition(missileXform) - radarPosition).Length() > Math.Min(missileBlip.MaxDistance, radarMax))
                continue;

            missiles.Add(new MissileVectorNetData(
                GetNetEntity(missile),
                seeker.MaxSpeed * 0.2f,
                Angle.FromDegrees(seeker.ScanArc)));
        }

        var saclosQuery = EntityQueryEnumerator<TargetGuidedComponent, RadarBlipComponent, TransformComponent>();
        while (saclosQuery.MoveNext(out var missile, out var seeker, out var missileBlip, out var missileXform))
        {
            if (!missileBlip.Enabled || !seeker.RadarLines)
                continue;

            if (missileXform.MapID != radarMapId)
                continue;

            if (IsHiddenByNebulaVeil(missile, missileXform, veilFields))
                continue;

            if ((_xform.GetWorldPosition(missileXform) - radarPosition).Length() > Math.Min(missileBlip.MaxDistance, radarMax))
                continue;

            missiles.Add(new MissileVectorNetData(
                GetNetEntity(missile),
                seeker.CurrentSpeed * 0.2f,
                Angle.Zero));
        }

        return missiles;
    }

    /// <summary>
    /// Assembles trajectory information for hitscan projectiles to be displayed on radar
    /// </summary>
    private List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> AssembleHitscanReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var hitscans = new List<(Vector2 Start, Vector2 End, float Thickness, Color Color)>();

        if (!Resolve(uid, ref component))
            return hitscans;

        var radarPosition = _xform.GetWorldPosition(uid);
        var radarMapId = Transform(uid).MapID;
        var veilFields = CollectActiveVeilFields(radarMapId);

        var hitscanQuery = EntityQueryEnumerator<HitscanRadarComponent>();

        while (hitscanQuery.MoveNext(out var hitscanUid, out var hitscan))
        {
            if (!hitscan.Enabled)
                continue;

            // Check if either the start or end point is within radar range
            var startDistance = (hitscan.StartPosition - radarPosition).Length();
            var endDistance = (hitscan.EndPosition - radarPosition).Length();

            if (startDistance > component.MaxRange && endDistance > component.MaxRange)
                continue;

            // Hide trails that start or end inside a Veil mid-zone.
            if (IsWorldPosInVeil(hitscan.StartPosition, veilFields) || IsWorldPosInVeil(hitscan.EndPosition, veilFields))
                continue;

            hitscans.Add((hitscan.StartPosition, hitscan.EndPosition, hitscan.LineThickness, hitscan.RadarColor));
        }

        return hitscans;
    }

    private List<(AmbientSpaceFieldComponent Field, Vector2 Pos)> CollectActiveVeilFields(MapId mapId)
    {
        var now = _timing.CurTime;
        if (_veilCache.TryGetValue(mapId, out var cached) && now - cached.BuiltAt < VeilCacheTtl)
            return cached.Fields;

        var fields = cached.Fields ?? new List<(AmbientSpaceFieldComponent, Vector2)>();
        fields.Clear();

        foreach (var uid in _hazardActivity.ActiveHazards)
        {
            if (!TryComp(uid, out AmbientSpaceFieldComponent? field))
                continue;

            if (!TryComp(uid, out TransformComponent? xform) || xform.MapID != mapId)
                continue;

            if (!FieldHasWeatherKind(field, NebulaWeatherKind.Veil))
                continue;

            fields.Add((field, _xform.GetWorldPosition(xform)));
        }

        _veilCache[mapId] = (now, fields);
        return fields;
    }

    private bool FieldHasWeatherKind(AmbientSpaceFieldComponent field, NebulaWeatherKind kind)
    {
        if (field.Weathers.Count > 0)
        {
            foreach (var weatherId in field.Weathers)
            {
                if (_prototypes.TryIndex(weatherId, out NebulaWeatherPrototype? weather) && weather.Kind == kind)
                    return true;
            }

            return false;
        }

        return field.Weather is { } fallbackId &&
               _prototypes.TryIndex(fallbackId, out NebulaWeatherPrototype? fallback) &&
               fallback.Kind == kind;
    }

    private bool IsHiddenByNebulaVeil(
        EntityUid blipUid,
        TransformComponent xform,
        List<(AmbientSpaceFieldComponent Field, Vector2 Pos)> veilFields)
    {
        if (HasComp<AmbientSpaceFieldComponent>(blipUid) || HasComp<SectorCelestialBodyComponent>(blipUid))
            return false;

        if (HasComp<NebulaVeilTrackedComponent>(blipUid))
            return true;

        if (xform.GridUid is { } grid && HasComp<NebulaVeilTrackedComponent>(grid))
            return true;

        if (veilFields.Count == 0)
            return false;

        return IsWorldPosInVeil(_xform.GetWorldPosition(xform), veilFields);
    }

    private static bool IsWorldPosInVeil(
        Vector2 worldPos,
        List<(AmbientSpaceFieldComponent Field, Vector2 Pos)> veilFields)
    {
        foreach (var (field, fieldPos) in veilFields)
        {
            if (NebulaVeilHelpers.IsInMidZone(field, fieldPos, worldPos))
                return true;
        }

        return false;
    }
}
