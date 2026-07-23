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

            // Lua: добавлен спавн частиц джета на сервере, копия с клиентского спавна для работающей кастомизации следа джетов. (Server-side jet particle spawning, replicated from the client-side spawn to enable jet trail customization.)
            // Настройка выбора серверного прототипа для спавна производится через JetpackComponent, переменную JetpackEffect. (The server-side prototype for spawning is configured via the JetpackComponent's JetpackEffect variable.
            try
            {
                var uidXform = Transform(uid);

                // Don't show particles unless the user is moving.
                if (Container.TryGetContainingContainer((uid, uidXform, null), out var container) &&
                    _entityManager.TryGetComponent(container.Owner, out PhysicsComponent? body) &&
                    body.LinearVelocity.LengthSquared() < 1f)
                {

                }
                else
                {
                    var coordinates = uidXform.Coordinates;
                    var gridUid =_transform.GetGrid(coordinates);

                    if (gridUid != null && _entityManager.TryGetComponent(gridUid, out MapGridComponent? grid))
                    {
                        coordinates = new EntityCoordinates(gridUid.Value, _mapSystem.WorldToLocal(gridUid.Value, grid, _transform.ToMapCoordinates(coordinates).Position));
                    }
                    else if (uidXform.MapUid != null)
                    {
                        coordinates = new EntityCoordinates(uidXform.MapUid.Value, _transform.GetWorldPosition(uidXform));
                    }
                    else
                    {
                        coordinates = default;
                    }

                    if (coordinates != default)
                    {
                        if (TryComp<JetpackComponent>(uid, out var jetpack))
                        {
                            Spawn(jetpack.JetpackEffect, coordinates);
                        }
                        else
                        {
                            Spawn("JetpackEffect", coordinates);
                        }
                    }
                }
            }
            catch
            {

            }

            _gasTank.UpdateUserInterface(gasTank);
        }

        foreach (var (uid, comp) in toDisable)
        {
            SetEnabled(uid, comp, false);
        }
    }
}
