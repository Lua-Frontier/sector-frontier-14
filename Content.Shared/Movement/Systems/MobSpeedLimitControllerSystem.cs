using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Movement.Systems;

public sealed class MobSpeedLimitControllerSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobSpeedLimitComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(EntityUid uid, MobSpeedLimitComponent component, ComponentInit args)
    {
        if (component.Enabled && TryComp<PhysicsComponent>(uid, out var physics))
        {
            var velocity = physics.LinearVelocity;
            var speed = velocity.Length();
            if (speed > component.MaxLinearVelocity)
            {
                var limitedVelocity = velocity.Normalized() * component.MaxLinearVelocity;
                _physics.SetLinearVelocity(uid, limitedVelocity, body: physics);
            }
        }
    }

    public void ApplySpeedLimit(EntityUid uid, MobSpeedLimitComponent? component = null)
    {
        if (!Resolve(uid, ref component) || !component.Enabled) return;

        if (!TryComp<PhysicsComponent>(uid, out var physics)) return;
        var velocity = physics.LinearVelocity;
        var speed = velocity.Length();
        if (speed > component.MaxLinearVelocity)
        {
            var limitedVelocity = velocity.Normalized() * component.MaxLinearVelocity;
            _physics.SetLinearVelocity(uid, limitedVelocity, body: physics);
        }
    }

    public float GetCurrentSpeed(EntityUid uid, PhysicsComponent? physics = null)
    {
        if (!Resolve(uid, ref physics)) return 0f;
        return physics.LinearVelocity.Length();
    }

    public bool IsExceedingSpeedLimit(EntityUid uid, MobSpeedLimitComponent? component = null, PhysicsComponent? physics = null)
    {
        if (!Resolve(uid, ref component) || !component.Enabled) return false;
        if (!Resolve(uid, ref physics)) return false;
        return physics.LinearVelocity.Length() > component.MaxLinearVelocity;
    }
}
