using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Rotation;
using Robust.Client.GameObjects;

namespace Content.Client.Kitchen;

public sealed class KitchenSpikeSystem : SharedKitchenSpikeSystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly Angle HookedRotation = Angle.FromDegrees(180);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KitchenSpikeHookedComponent, ComponentStartup>(OnHookedStartup);
        SubscribeLocalEvent<KitchenSpikeHookedComponent, ComponentShutdown>(OnHookedShutdown);
    }

    private void OnHookedStartup(Entity<KitchenSpikeHookedComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.EnableDirectionOverride = true;
        sprite.DirectionOverride = Direction.South;
    }

    private void OnHookedShutdown(Entity<KitchenSpikeHookedComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp<SpriteComponent>(ent, out var sprite))
            sprite.EnableDirectionOverride = false;

        if (TryComp<RotationVisualsComponent>(ent, out var rotation))
            rotation.HorizontalRotation = rotation.DefaultRotation;

        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.QueueUpdate(ent, appearance);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<KitchenSpikeHookedComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (TryComp<RotationVisualsComponent>(uid, out var rotation))
                rotation.HorizontalRotation = HookedRotation;

            if (!sprite.Rotation.Equals(HookedRotation))
                _sprite.SetRotation((uid, sprite), HookedRotation);
        }
    }
}
