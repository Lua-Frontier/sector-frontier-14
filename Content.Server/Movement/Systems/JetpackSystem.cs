using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Mono.Radar;
using Content.Shared.Atmos.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Movement.Systems;

public sealed class JetpackSystem : SharedJetpackSystem
{
    private static readonly EntProtoId RadarTrailProto = "JetpackRadarTrail";

    [Dependency] private readonly GasTankSystem _gasTank = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Mono: toggle radar signature with jetpack activation
        SubscribeLocalEvent<ActiveJetpackComponent, ComponentStartup>(OnJetpackActivated);
        SubscribeLocalEvent<ActiveJetpackComponent, ComponentShutdown>(OnJetpackDeactivated);
    }

    protected override bool CanEnable(EntityUid uid, JetpackComponent component)
    {
        return base.CanEnable(uid, component) &&
               TryComp<GasTankComponent>(uid, out var gasTank) &&
               !(gasTank.Air.TotalMoles < component.MoleUsage);
    }

    private void OnJetpackActivated(EntityUid uid, ActiveJetpackComponent component, ComponentStartup args)
    {
        if (!TryComp<JetpackComponent>(uid, out var jetpack) || !jetpack.RadarBlip)
            return;

        // Prefer enabling an existing prototype blip; otherwise create a cyan EVA signature.
        if (TryComp<RadarBlipComponent>(uid, out var existing))
        {
            existing.Enabled = true;
            return;
        }

        var blip = EnsureComp<RadarBlipComponent>(uid);
        blip.RadarColor = Color.Cyan;
        blip.Scale = 0.5f;
        blip.Shape = RadarBlipShape.Circle;
        blip.VisibleFromOtherGrids = true;
        blip.RequireNoGrid = true;
        blip.Enabled = true;
        blip.MaxDistance = 256f;
    }

    private void OnJetpackDeactivated(EntityUid uid, ActiveJetpackComponent component, ComponentShutdown args)
    {
        if (!TryComp<JetpackComponent>(uid, out var jetpack) || !jetpack.RadarBlip)
            return;

        // Prototype jetpacks keep RadarBlip but hide while off.
        if (TryComp<RadarBlipComponent>(uid, out var blip) &&
            MetaData(uid).EntityPrototype?.Components.ContainsKey("RadarBlip") == true)
        {
            blip.Enabled = false;
            return;
        }

        RemComp<RadarBlipComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toDisable = new ValueList<(EntityUid Uid, JetpackComponent Component)>();
        var query = EntityQueryEnumerator<ActiveJetpackComponent, JetpackComponent, GasTankComponent>();

        while (query.MoveNext(out var uid, out var active, out var comp, out var gasTankComp))
        {
            if (_timing.CurTime < active.TargetTime)
                continue;

            var gasTank = (uid, gasTankComp);
            active.TargetTime = _timing.CurTime + TimeSpan.FromSeconds(active.EffectCooldown);
            var usedAir = _gasTank.RemoveAir(gasTank, comp.MoleUsage);

            if (usedAir == null)
                continue;

            var usedEnoughAir =
                MathHelper.CloseTo(usedAir.TotalMoles, comp.MoleUsage, comp.MoleUsage / 100);

            if (!usedEnoughAir)
                toDisable.Add((uid, comp));

            _gasTank.UpdateUserInterface(gasTank);
            if (comp.RadarBlip)
                TrySpawnRadarTrail(uid, comp);
        }

        foreach (var (uid, comp) in toDisable)
        { SetEnabled(uid, comp, false); }
    }
    private void TrySpawnRadarTrail(EntityUid uid, JetpackComponent jetpack)
    {
        var xform = Transform(uid);

        if (Container.TryGetContainingContainer((uid, xform, null), out var container) &&
            TryComp(container.Owner, out PhysicsComponent? body) && body.LinearVelocity.LengthSquared() < 1f)
        { return; }
        if (!TryGetTrailCoordinates(uid, xform, out var coordinates))
            return;
        var trail = Spawn(RadarTrailProto, coordinates);
        if (!TryComp(trail, out RadarBlipComponent? blip))
            return;
        if (_prototypes.TryIndex(jetpack.JetpackEffect, out EntityPrototype? effectProto) &&
            effectProto.TryGetComponent<RadarBlipComponent>(out var template, EntityManager.ComponentFactory))
        {
            blip.RadarColor = template.RadarColor;
            blip.HighlightedRadarColor = template.HighlightedRadarColor;
            blip.Scale = template.Scale;
            blip.Shape = template.Shape;
            blip.VisibleFromOtherGrids = template.VisibleFromOtherGrids;
            blip.RequireNoGrid = template.RequireNoGrid;
            blip.MaxDistance = template.MaxDistance;
            blip.Enabled = true;
        }
    }

    private bool TryGetTrailCoordinates(EntityUid uid, TransformComponent xform, out EntityCoordinates coordinates)
    {
        coordinates = xform.Coordinates;
        var gridUid = _transform.GetGrid(coordinates);

        if (gridUid != null && TryComp(gridUid, out MapGridComponent? grid))
        {
            coordinates = new EntityCoordinates(
                gridUid.Value, _mapSystem.WorldToLocal(gridUid.Value, grid, _transform.ToMapCoordinates(coordinates).Position));
            return true;
        }

        if (xform.MapUid != null)
        {
            coordinates = new EntityCoordinates(xform.MapUid.Value, _transform.GetWorldPosition(xform));
            return true;
        }

        coordinates = default;
        return false;
    }
}
