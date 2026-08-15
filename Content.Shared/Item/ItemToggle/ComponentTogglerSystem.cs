using Content.Shared.Item.ItemToggle.Components;

namespace Content.Shared.Item.ItemToggle;

/// <summary>
/// Handles <see cref="ComponentTogglerComponent"/> component manipulation.
/// </summary>
public sealed class ComponentTogglerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComponentTogglerComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnToggled(Entity<ComponentTogglerComponent> ent, ref ItemToggledEvent args)
    {
        ToggleComponent(ent.Owner, args.Activated, ent.Comp);
    }

    public void ToggleComponent(EntityUid uid, bool activated, ComponentTogglerComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (activated)
        {
            var target = component.Parent ? Transform(uid).ParentUid : uid;

            if (TerminatingOrDeleted(target))
                return;

            component.Target = target;

            EntityManager.AddComponents(target, component.Components);
        }
        else
        {
            if (component.Target == null)
                return;

            if (TerminatingOrDeleted(component.Target.Value))
                return;

            EntityManager.RemoveComponents(component.Target.Value, component.RemoveComponents ?? component.Components);
        }
    }
}
