using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server.Movement.Systems;

public sealed class JetpackSystem : SharedJetpackSystem
{
    [Dependency] private readonly GasTankSystem _gasTank = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

    }
    // NOTE / ПРИМЕЧАНИЕ:
    // Particles for the jetpack were originally spawned on the client side, which means
    // they are not visible to server-side systems (for example radar blip logic).
    // To ensure server-side systems can see and react to the jetpack effect, we spawn
    // the particle/prototype entities on the server instead of the client.
    //
    // Частицы джетпака изначально создавались на клиенте, поэтому серверные системы
    // (например, отображение на радаре) не видели эти сущности. Чтобы сервер мог
    // учитывать и реагировать на эффект джетпака, мы создаём сущности эффектов на
    // стороне сервера.

    protected override bool CanEnable(EntityUid uid, JetpackComponent component)
    {
        return base.CanEnable(uid, component) &&
               TryComp<GasTankComponent>(uid, out var gasTank) &&
               !(gasTank.Air.TotalMoles < component.MoleUsage);
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
                MathHelper.CloseTo(usedAir.TotalMoles, comp.MoleUsage, comp.MoleUsage/100);

            if (!usedEnoughAir)
            {
                toDisable.Add((uid, comp));
            }

            _gasTank.UpdateUserInterface(gasTank);
        }

        foreach (var (uid, comp) in toDisable)
        {
            SetEnabled(uid, comp, false);
        }

        // Server-side particle spawning for active jetpacks (серверный спавн частиц для активных джетпаков)
        var particleQuery = EntityQueryEnumerator<ActiveJetpackComponent, TransformComponent>();

        while (particleQuery.MoveNext(out var uidP, out var active, out var xform))
        {
            // Check movement-based cooldown similar to client logic(проверяем, прошло ли достаточно времени с последнего спавна частиц, если игрок не двигался)
            if (_transform.InRange(xform.Coordinates, active.LastCoordinates, active.MaxDistance))
            {
                if (_timing.CurTime < active.TargetTime)
                    continue;
            }

            active.LastCoordinates = _transform.GetMoverCoordinates(xform.Coordinates);
            active.TargetTime = _timing.CurTime + TimeSpan.FromSeconds(active.EffectCooldown);

            // Don't spawn particles if the jetpack user/holder isn't moving (не спавним частицы, если пользователь джетпака не двигается)
            if (Container.TryGetContainingContainer((uidP, xform, null), out var container) &&
                TryComp<PhysicsComponent>(container.Owner, out var body) &&
                body.LinearVelocity.LengthSquared() < 1f)
            {
                continue;
            }

            var coordinates = xform.Coordinates;
            var gridUid = _transform.GetGrid(coordinates);

            if (TryComp<MapGridComponent>(gridUid, out var grid))
            {
                coordinates = new EntityCoordinates(gridUid.Value, _mapSystem.WorldToLocal(gridUid.Value, grid, _transform.ToMapCoordinates(coordinates).Position));
            }
            else if (xform.MapUid != null)
            {
                coordinates = new EntityCoordinates(xform.MapUid.Value, _transform.GetWorldPosition(xform));
            }
            else
            {
                continue;
            }

            // Choose prototype from JetpackComponent if available, fallback to default (выбираем прототип из JetpackComponent, если он есть, иначе используем дефолтный как затычку)
            var proto = "JetpackEffect";
            if (TryComp<JetpackComponent>(uidP, out var jetpack))
                proto = jetpack.JetpackEffect;

            // Spawn the effect on the server so server systems can see it(спавним эффект на сервере, чтобы серверные другие системы могли его видеть)
            Spawn(proto, coordinates);
        }
    }
}
