using Content.Shared.Clothing.Components;
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

        SubscribeLocalEvent<RaceRequirementComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RaceRequirementComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
    }

    private void OnExamine(EntityUid uid, RaceRequirementComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // print current armor status
        string examineMsg;
        if (!component.Enabled)
        {
            examineMsg = "race-requirement-component-disabled";
        }
        else
        {
            if (IsValidRace(args.Examiner, uid))
                examineMsg = "race-requirement-component-canequip";
            else
                examineMsg = "race-requirement-component-cantequip";
        }
        args.PushMarkup(Loc.GetString(examineMsg));
    }

    private void OnEquipAttempt(EntityUid uid, RaceRequirementComponent component, BeingEquippedAttemptEvent args)
    {
        var isValid = IsValidRace(args.EquipTarget, uid, component);
        if (!isValid)
        {
            args.Reason = component.AllowedRaces != null && component.AllowedRaces.Count > 0
                ? "race-requirement-component-equip-failed-allowed"
                : "race-requirement-component-equip-failed";
            args.Cancel();
        }
    }
    public bool IsValidRace(EntityUid wearerUid, EntityUid itemUid, RaceRequirementComponent? component = null)
    {
        if (!Resolve(itemUid, ref component))
            return false;

        // Отключено — пропускаем.
        if (!component.Enabled)
            return true;

        if (component.AllowedRaces == null || component.AllowedRaces.Count == 0)
            return true;

        if (!TryComp<HumanoidAppearanceComponent>(wearerUid, out var appearance))
            return false;

        // Проверяем, есть ли раса в списке
        return component.AllowedRaces.Contains(appearance.Species);
    }
}
