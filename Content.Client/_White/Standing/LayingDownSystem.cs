using Content.Shared._White.Standing;
using Content.Shared.Buckle;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._White.Standing;

public sealed class LayingDownSystem : SharedLayingDownSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LayingDownComponent, MoveEvent>(OnMovementInput);

        SubscribeNetworkEvent<CheckAutoGetUpEvent>(OnCheckAutoGetUp);
    }

    private void OnMovementInput(EntityUid uid, LayingDownComponent component, MoveEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!_standing.IsDown(uid))
            return;

        if (_buckle.IsBuckled(uid))
            return;

        if (_animation.HasRunningAnimation(uid, "rotate"))
            return;

        if (!TryComp<TransformComponent>(uid, out var transform)
            || !TryComp<SpriteComponent>(uid, out var sprite)
            || !TryComp<RotationVisualsComponent>(uid, out var rotationVisuals))
        {
            return;
        }

        var rotation = transform.LocalRotation + (_eyeManager.CurrentEye.Rotation - (transform.LocalRotation - transform.WorldRotation));

        // Lua start
        var horizontal = rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North
            ? Angle.FromDegrees(270)
            : Angle.FromDegrees(90);

        rotationVisuals.HorizontalRotation = horizontal;

        if (sprite.Rotation.EqualsApprox(rotationVisuals.VerticalRotation))
            return;

        sprite.Rotation = horizontal;
        // Lua end
    }

    private void OnCheckAutoGetUp(CheckAutoGetUpEvent ev, EntitySessionEventArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var uid = GetEntity(ev.User);

        if (!TryComp<TransformComponent>(uid, out var transform) || !TryComp<RotationVisualsComponent>(uid, out var rotationVisuals))
            return;

        var rotation = transform.LocalRotation + (_eyeManager.CurrentEye.Rotation - (transform.LocalRotation - transform.WorldRotation));

        if (rotation.GetDir() is Direction.SouthEast or Direction.East or Direction.NorthEast or Direction.North)
        {
            rotationVisuals.HorizontalRotation = Angle.FromDegrees(270);
            return;
        }

        rotationVisuals.HorizontalRotation = Angle.FromDegrees(90);
    }
}
