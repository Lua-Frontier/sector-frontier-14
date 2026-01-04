using Content.Server.Chemistry.Components;
using Content.Server.Temperature.Components;
using Content.Shared._Goobstation.Vehicles;
using Content.Shared._Mono.Radar;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Mono.Radar;

public sealed partial class RadarBlipSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);
        SubscribeLocalEvent<RadarBlipComponent, ComponentShutdown>(OnBlipShutdown);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var jetpackQuery = EntityQueryEnumerator<ActiveJetpackComponent, RadarBlipComponent>();
        while (jetpackQuery.MoveNext(out var uid, out _, out var blip))
        {
            var t = _timing.CurTime;
            var on = (t.TotalSeconds % 5.0) < 0.5;
            blip.Enabled = on;
        }
        var vehicleQuery = EntityQueryEnumerator<VehicleComponent, RadarBlipComponent>();
        while (vehicleQuery.MoveNext(out var uid, out var vehicle, out var blip))
        {
            if (vehicle.Driver == null)
            { blip.Enabled = false; continue; }
            var t = _timing.CurTime;
            var on = (t.TotalSeconds % 5.0) < 0.5;
            blip.Enabled = on;
        }
        var mobPulseQuery = EntityQueryEnumerator<Content.Shared.Mobs.Components.MobStateComponent, RadarBlipComponent>();
        while (mobPulseQuery.MoveNext(out var uid, out _, out var blip))
        {
            var t = _timing.CurTime;
            var on = (t.TotalSeconds % 5.0) < 0.5;
            blip.Enabled = on;
        }
        var mobQuery = EntityQueryEnumerator<Content.Shared.Mobs.Components.MobStateComponent>();
        while (mobQuery.MoveNext(out var mobUid, out _))
        {
            if (!HasComp<RadarBlipComponent>(mobUid))
            {
                var rb = EnsureComp<RadarBlipComponent>(mobUid);
                rb.VisibleFromOtherGrids = false;
                rb.RequireNoGrid = true;
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
        var hitscans = AssembleHitscanReport((EntityUid)radarUid, radar);

        // Combine the blips and hitscan lines
        var giveEv = new GiveBlipsEvent(blips, hitscans);
        RaiseNetworkEvent(giveEv, args.SenderSession);

        blips.Clear();
        hitscans.Clear();
    }

    private void OnBlipShutdown(EntityUid blipUid, RadarBlipComponent component, ComponentShutdown args)
    {
        var netBlipUid = GetNetEntity(blipUid);
        var removalEv = new BlipRemovalEvent(netBlipUid);
        RaiseNetworkEvent(removalEv);
    }

    private List<(NetEntity netUid, NetCoordinates Position, Vector2 Vel, float Scale, Color Color, RadarBlipShape Shape)> AssembleBlipsReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var blips = new List<(NetEntity netUid, NetCoordinates Position, Vector2 Vel, float Scale, Color Color, RadarBlipShape Shape)>();

        if (Resolve(uid, ref component))
        {
            var radarXform = Transform(uid);
            var radarPosition = _xform.GetWorldPosition(uid);
            var radarGrid = _xform.GetGrid(uid);
            var radarMapId = radarXform.MapID;

            // Check if the radar is on an FTL map
            var isFtlMap = HasComp<FTLComponent>(radarXform.GridUid);

            var blipQuery = EntityQueryEnumerator<RadarBlipComponent, TransformComponent, PhysicsComponent>();

            while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform, out var blipPhysics))
            {
                if (!blip.Enabled)
                    continue;

                // This prevents blips from showing on radars that are on different maps
                if (blipXform.MapID != radarMapId)
                    continue;

                var netBlipUid = GetNetEntity(blipUid);

                var blipGrid = blipXform.GridUid;

                // if (HasComp<CircularShieldRadarComponent>(blipUid))
                // {
                //     // Skip if in FTL
                //     if (isFtlMap)
                //         continue;
                //
                //     // Skip if no grid
                //     if (blipGrid == null)
                //         continue;
                //
                //     // Ensure the grid is a valid MapGrid
                //     if (!HasComp<MapGridComponent>(blipGrid.Value))
                //         continue;
                //
                //     // Ensure the shield is a direct child of the grid
                //     if (blipXform.ParentUid != blipGrid)
                //         continue;
                // }

                var blipVelocity = _physics.GetMapLinearVelocity(blipUid, blipPhysics, blipXform);

                var distance = (_xform.GetWorldPosition(blipXform) - radarPosition).Length();

                float maxDistance = blip.MaxDistance;
                if (HasComp<VaporComponent>(blipUid))
                { maxDistance = 160f; }
                else if (HasComp<MobStateComponent>(blipUid))
                {
                    if (!TryComp<TemperatureComponent>(blipUid, out var temp)) continue;
                    var tempC = temp.CurrentTemperature - 273.15f;
                    if (tempC <= 0f) continue;
                    var distDyn = 30f + tempC * 1.9f;
                    if (distDyn < 25f) distDyn = 25f;
                    if (distDyn > 160f) distDyn = 160f;
                    maxDistance = distDyn;
                }
                else if (TryComp<TemperatureComponent>(blipUid, out var temp))
                {
                    var tempC = temp.CurrentTemperature - 273.15f;
                    if (tempC <= 0f) { continue; }
                    var distDyn = 30f + tempC * 1.9f;
                    if (distDyn < 25f) distDyn = 25f;
                    if (distDyn > 160f) distDyn = 160f;
                    maxDistance = distDyn;
                }
                var radarMax = component?.MaxRange ?? SharedRadarConsoleSystem.DefaultMaxRange;
                var allowedDistance = Math.Min(maxDistance, radarMax);
                if (distance > allowedDistance) continue;
                if ((blip.RequireNoGrid && blipGrid != null) || (!blip.VisibleFromOtherGrids && blipGrid != radarGrid)) continue;

                // due to PVS being a thing, things will break if we try to parent to not the map or a grid
                var coord = blipXform.Coordinates;
                if (blipXform.ParentUid != blipXform.MapUid && blipXform.ParentUid != blipGrid)
                    coord = _xform.WithEntityId(coord, blipGrid ?? blipXform.MapUid!.Value);
                // we're parented to either the map or a grid and this is relative velocity so account for grid movement
                if (blipGrid != null)
                    blipVelocity -= _physics.GetLinearVelocity(blipGrid.Value, coord.Position);

                blips.Add((netBlipUid, GetNetCoordinates(coord), blipVelocity, blip.Scale, blip.RadarColor, blip.Shape));
            }
        }

        return blips;
    }

    /// <summary>
    /// Assembles trajectory information for hitscan projectiles to be displayed on radar
    /// </summary>
    private List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> AssembleHitscanReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var hitscans = new List<(Vector2 Start, Vector2 End, float Thickness, Color Color)>();

        if (!Resolve(uid, ref component))
            return hitscans;

        var radarXform = Transform(uid);
        var radarPosition = _xform.GetWorldPosition(uid);
        var radarGrid = _xform.GetGrid(uid);
        var radarMapId = radarXform.MapID;

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

            hitscans.Add((hitscan.StartPosition, hitscan.EndPosition, hitscan.LineThickness, hitscan.RadarColor));
        }

        return hitscans;
    }

    /// <summary>
    /// Configures the radar blip for a vehicle entity.
    /// </summary>
    public void SetupVehicleRadarBlip(Entity<VehicleComponent> uid)
    {
        if (TryComp<RadarBlipComponent>(uid, out var blip)) blip.Enabled = true;
    }
}
