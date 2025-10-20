using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Movement.Systems;

public sealed class MobSpeedLimitSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobSpeedLimitComponent, PhysicsUpdateAfterSolveEvent>(OnPhysicsUpdate);
    }

    private void OnPhysicsUpdate(EntityUid uid, MobSpeedLimitComponent component, ref PhysicsUpdateAfterSolveEvent args)
    {
        if (!_cfg.GetCVar(CCVars.MobSpeedLimitEnabled)) return;
        if (!component.Enabled) return;
        if (!TryComp<PhysicsComponent>(uid, out var physics)) return;
        var velocity = physics.LinearVelocity;
        var speed = velocity.Length();
        var maxVelocity = component.MaxLinearVelocity > 0 ? component.MaxLinearVelocity : _cfg.GetCVar(CCVars.MobMaxLinearVelocity);
        if (speed > maxVelocity)
        {
            var limitedVelocity = velocity.Normalized() * maxVelocity;
            _physics.SetLinearVelocity(uid, limitedVelocity, body: physics);
        }
    }

    public void SetMaxVelocity(EntityUid uid, float maxVelocity, MobSpeedLimitComponent? component = null)
    {
        if (!Resolve(uid, ref component)) return;
        component.MaxLinearVelocity = maxVelocity;
        Dirty(uid, component);
    }

    public void SetEnabled(EntityUid uid, bool enabled, MobSpeedLimitComponent? component = null)
    {
        if (!Resolve(uid, ref component)) return;
        component.Enabled = enabled;
        Dirty(uid, component);
    }
}
