using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server._NF.Solar.Components;
using Content.Server._Lua.Stargate.Components;
using Content.Shared;
using Content.Shared.GameTicking;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._NF.Solar.EntitySystems;

/// <summary>
///     Responsible for maintaining the solar-panel sun angle and updating <see cref='NFSolarPanelComponent'/> coverage.
///     Keeps track of per-grid solar panel angle and velocity using <see cref='SolarPoweredGridComponent'/>.
///     Largely based on upstream's PowerSolarSystem (with many thanks to 20kdc, DrSmugleaf and others)
/// </summary>
[UsedImplicitly]
internal sealed class NFPowerSolarSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!; // Frontier

    /// <summary>
    /// Maximum panel angular velocity range - used to stop people rotating panels fast enough that the lag prevention becomes noticable
    /// </summary>
    public const float MaxPanelVelocityDegrees = 1f;

    /// <summary>
    /// The current sun angle.
    /// </summary>
    public Angle TowardsSun = Angle.Zero;

    /// <summary>
    /// The current sun angular velocity. (This is changed in Initialize)
    /// </summary>
    public Angle SunAngularVelocity = Angle.Zero;

    /// <summary>
    /// The distance before the sun is considered to have been 'visible anyway'.
    /// This value, like the occlusion semantics, is borrowed from all the other SS13 stations with solars.
    /// </summary>
    public float SunOcclusionCheckDistance = 20;

    /// <summary>
    /// Queue of panels to update each cycle.
    /// </summary>
    private readonly Queue<Entity<NFSolarPanelComponent>> _updateQueue = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<NFSolarPanelComponent, MapInitEvent>(OnPanelMapInit);
        SubscribeLocalEvent<SolarPoweredGridComponent, MapInitEvent>(OnSolarPoweredGridMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(Reset);
        RandomizeSun();
    }

    public void Reset(RoundRestartCleanupEvent ev)
    {
        RandomizeSun();
    }

    private void RandomizeSun()
    {
        // Initialize the sun to something random
        TowardsSun = MathHelper.TwoPi * _robustRandom.NextDouble();
        SunAngularVelocity = Angle.FromDegrees(0.125 + (_robustRandom.NextDouble() - 0.5) * 0.1); // 0.075/s - 0.175/s (4800s - ~2000s per orbit)
        if (_robustRandom.Prob(0.5f))
            SunAngularVelocity = -SunAngularVelocity; // retrograde rotation(?)
    }

    private void OnPanelMapInit(EntityUid uid, NFSolarPanelComponent component, MapInitEvent args)
    {
        UpdateSupply(uid, component);
    }

    private void OnSolarPoweredGridMapInit(EntityUid uid, SolarPoweredGridComponent component, MapInitEvent args)
    {
        if (component.TrackOnInit)
        {
            var xform = Transform(uid);
            component.TargetPanelRotation = GetSunAngle(xform.MapID);
            component.TargetPanelVelocity = GetSunAngularVelocity(xform.MapID);
        }
    }

    public override void Update(float frameTime)
    {
        TowardsSun += SunAngularVelocity * frameTime;
        TowardsSun = TowardsSun.Reduced();

        if (_updateQueue.Count > 0)
        {
            UpdateSolarGridRotations(false, frameTime); // Frontier
            var panel = _updateQueue.Dequeue();
            if (panel.Comp.Running)
                UpdatePanelCoverage(panel);
        }
        else
        {
            UpdateSolarGridRotations(true, frameTime); // Frontier

            var query = EntityQueryEnumerator<NFSolarPanelComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var panel, out var xform))
            {
                if (xform.GridUid == null)
                    continue;

                var poweredGridComp = EnsureComp<SolarPoweredGridComponent>(xform.GridUid.Value);
                poweredGridComp.TotalPanelPower += panel.MaxSupply * panel.Coverage;
                poweredGridComp.LastUpdatedTick = _gameTiming.CurTick.Value;
                _transformSystem.SetWorldRotation(xform, poweredGridComp.TargetPanelRotation);
                _updateQueue.Enqueue((uid, panel));
            }

            // Cull grid set
            var gridQuery = EntityQueryEnumerator<SolarPoweredGridComponent>();
            while (gridQuery.MoveNext(out var uid, out var gridPower))
            {
                if (!gridPower.DoNotCull &&
                    gridPower.LastUpdatedTick != _gameTiming.CurTick.Value)
                {
                    RemCompDeferred<SolarPoweredGridComponent>(uid);
                }
            }
        }
    }

    // Adjusts all grid rotations at their current tracking velocity and optionally resets their total power.
    private void UpdateSolarGridRotations(bool resetPower, float dt)
    {
        var gridQuery = EntityQueryEnumerator<SolarPoweredGridComponent>();
        while (gridQuery.MoveNext(out _, out var grid))
        {
            if (resetPower)
                grid.TotalPanelPower = 0;

            grid.TargetPanelRotation += grid.TargetPanelVelocity * dt;
            grid.TargetPanelRotation = grid.TargetPanelRotation.Reduced();
        }
    }

    // Currently verbatim from PowerSolarSystem.UpdatePanelCoverage
    private void UpdatePanelCoverage(Entity<NFSolarPanelComponent> panel)
    {
        var entity = panel.Owner;
        var xform = EntityManager.GetComponent<TransformComponent>(entity);
        var towardsSun = GetSunAngle(xform.MapID);
        var sunlightLevel = GetSunlightLevel(xform.MapID);

        // So apparently, and yes, I *did* only find this out later,
        // this is just a really fancy way of saying "Lambert's law of cosines".
        // ...I still think this explaination makes more sense.

        // In the 'sunRelative' coordinate system:
        // the sun is considered to be an infinite distance directly up.
        // this is the rotation of the panel relative to that.
        // directly upwards (theta = 0) = coverage 1
        // left/right 90 degrees (abs(theta) = (pi / 2)) = coverage 0
        // directly downwards (abs(theta) = pi) = coverage -1
        // as TowardsSun + = CCW,
        // panelRelativeToSun should - = CW
        var panelRelativeToSun = _transformSystem.GetWorldRotation(xform) - towardsSun;
        // essentially, given cos = X & sin = Y & Y is 'downwards',
        // then for the first 90 degrees of rotation in either direction,
        // this plots the lower-right quadrant of a circle.
        // now basically assume a line going from the negated X/Y to there,
        // and that's the hypothetical solar panel.
        //
        // since, again, the sun is considered to be an infinite distance upwards,
        // this essentially means Cos(panelRelativeToSun) is half of the cross-section,
        // and since the full cross-section has a max of 2, effectively-halving it is fine.
        //
        // as for when it goes negative, it only does that when (abs(theta) > pi)
        // and that's expected behavior.
        float coverage = (float)Math.Max(0, Math.Cos(panelRelativeToSun));
        coverage *= sunlightLevel;

        if (coverage > 0 && ShouldCheckOcclusion(xform.MapID))
        {
            // Determine if the solar panel is occluded, and zero out coverage if so.
            var ray = new CollisionRay(_transformSystem.GetWorldPosition(xform), towardsSun.ToWorldVec(), (int)CollisionGroup.Opaque);
            var rayCastResults = _physicsSystem.IntersectRayWithPredicate(
                xform.MapID,
                ray,
                SunOcclusionCheckDistance,
                e => !xform.Anchored || e == entity);
            if (rayCastResults.Any())
                coverage = 0;
        }

        // Total coverage calculated; apply it to the panel.
        panel.Comp.Coverage = coverage;
        UpdateSupply(panel, panel);
    }

    public void UpdateSupply(
        EntityUid uid,
        NFSolarPanelComponent? solar = null,
        PowerSupplierComponent? supplier = null)
    {
        if (!Resolve(uid, ref solar, ref supplier, false))
            return;

        supplier.MaxSupply = (int)(solar.MaxSupply * solar.Coverage);
    }

    private bool ShouldCheckOcclusion(MapId mapId)
    {
        var mapUid = _mapManager.GetMapEntityId(mapId);
        if (mapUid == EntityUid.Invalid)
            return true;
        return !HasComp<StargateDestinationComponent>(mapUid);
    }

    public Angle GetSunAngle(MapId mapId)
    {
        if (!TryGetPlanetarySolarState(mapId, out var angle, out _, out _))
            return TowardsSun;

        return angle;
    }

    public Angle GetSunAngularVelocity(MapId mapId)
    {
        if (!TryGetPlanetarySolarState(mapId, out _, out var velocity, out _))
            return SunAngularVelocity;

        return velocity;
    }

    public float GetSunlightLevel(MapId mapId)
    {
        if (!TryGetPlanetarySolarState(mapId, out _, out _, out var lightLevel))
            return 1f;

        return lightLevel;
    }

    private bool TryGetPlanetarySolarState(MapId mapId, out Angle towardsSun, out Angle angularVelocity, out float sunlightLevel)
    {
        towardsSun = TowardsSun;
        angularVelocity = SunAngularVelocity;
        sunlightLevel = 1f;

        var mapUid = _mapManager.GetMapEntityId(mapId);
        if (mapUid == EntityUid.Invalid ||
            !TryComp<LightCycleComponent>(mapUid, out var cycle))
        { return false; }
        var duration = MathF.Max(1f, (float)cycle.Duration.TotalSeconds);
        var elapsed = (float)(_gameTiming.CurTime
            .Add(cycle.Offset)
            .Subtract(_gameTicker.RoundStartTimeSpan)
            .Subtract(_metaData.GetPauseTime(mapUid))
            .TotalSeconds);
        var wrappedTime = elapsed % duration;
        if (wrappedTime < 0) wrappedTime += duration;
        towardsSun = Angle.FromDegrees(360f * (wrappedTime / duration)).Reduced();
        angularVelocity = Angle.FromDegrees(360f / duration);
        sunlightLevel = cycle.Enabled
            ? Math.Clamp((float)SharedLightCycleSystem.CalculateLightLevel(cycle, elapsed), 0f, 1f)
            : 1f;
        return true;
    }
}
