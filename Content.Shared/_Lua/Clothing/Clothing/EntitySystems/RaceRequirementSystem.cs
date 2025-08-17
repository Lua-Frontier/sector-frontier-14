using Content.Shared.Clothing.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Content.Shared.Humanoid;

namespace Content.Shared._Lua.Clothing.EntitySystems;

public sealed class RaceRequirementSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RaceRequirementComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
    }
    private void OnEquipAttempt(EntityUid uid, RaceRequirementComponent component, BeingEquippedAttemptEvent args)
    {
        var isValid = IsValidRace(args.EquipTarget, uid, component);
        if (!isValid)
        {
            if (component.AllowedRaces != null && component.AllowedRaces.Count > 0)
            {
                args.Reason = $"Вы не рассы: {string.Join(", ", component.AllowedRaces)}";
            }
            else
            {
                args.Reason = "race requirement failed";
            }
            args.Cancel();
        }
    }
    public bool IsValidRace(EntityUid wearerUid, EntityUid itemUid, RaceRequirementComponent? component = null)
    {
        if (!Resolve(itemUid, ref component))
            return false;

        if (component.AllowedRaces == null || component.AllowedRaces.Count == 0)
            return true;

        if (!TryComp<HumanoidAppearanceComponent>(wearerUid, out var appearance))
            return false;

        // Проверяем, есть ли раса в списке
        return component.AllowedRaces.Contains(appearance.Species);
    }
}
