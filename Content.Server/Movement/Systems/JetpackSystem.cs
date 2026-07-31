using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Mono.Radar;
using Content.Shared.Atmos.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Timing;

namespace Content.Server.Movement.Systems;

public sealed class JetpackSystem : SharedJetpackSystem
{
    [Dependency] private readonly GasTankSystem _gasTank = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

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
        }

        foreach (var (uid, comp) in toDisable)
        {
            SetEnabled(uid, comp, false);
        }
    }
}
