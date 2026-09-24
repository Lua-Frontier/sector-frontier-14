using Content.Server.Body.Systems;
using Content.Server.DoAfter;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Popups;
using Content.Lua.Shared.Demon;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Verbs;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System;

namespace Content.Lua.Server.Demon;

public sealed class ArkanaBloodDrinkSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StomachSystem _stomach = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private static readonly TimeSpan DrinkDelay = TimeSpan.FromSeconds(3);
    private const float StartPiercingDamage = 0.7f;
    private static readonly FixedPoint2 DrainAmount = FixedPoint2.New(25);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerb);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ArkanaBloodDrinkDoAfterEvent>(OnDoAfter);
    }

    private void OnGetAltVerb(EntityUid uid, HumanoidAppearanceComponent targetAppearance, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(args.User, out var userAppearance) || userAppearance.Species != "Demon")
            return;

        var species = targetAppearance.Species;
        if (species != "Human" && species != "Dwarf")
            return;

        if (!HasComp<BloodstreamComponent>(uid))
            return;

        AlternativeVerb verb = new()
        {
            Text = Loc.GetString("arkana-verb-drink-blood"),
            Act = () => StartDrink(args.User, uid),
            Priority = 2
        };
        args.Verbs.Add(verb);
    }

    private void StartDrink(EntityUid user, EntityUid target)
    {
        var args = new DoAfterArgs(EntityManager, user, DrinkDelay, new ArkanaBloodDrinkDoAfterEvent(), user, target, null)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.25f,
            DistanceThreshold = 1.25f,
            NeedHand = false
        };

        var pierce = new DamageSpecifier(_proto.Index<DamageTypePrototype>("Piercing"), StartPiercingDamage);
        _damageableSystem.TryChangeDamage(target, pierce, ignoreResistances: false, interruptsDoAfters: false, origin: user);

        _doAfter.TryStartDoAfter(args);
    }

    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    private void OnDoAfter(EntityUid uid, HumanoidAppearanceComponent userAppearance, ref ArkanaBloodDrinkDoAfterEvent ev)
    {
        if (ev.Cancelled || ev.Handled)
            return;

        var user = ev.Args.User;
        var maybeTarget = ev.Args.Target;
        if (maybeTarget is not EntityUid target)
            return;

        if (userAppearance.Species != "Demon")
            return;
        if (!TryComp<HumanoidAppearanceComponent>(target, out var targetAppearance))
            return;
        var species = targetAppearance.Species;
        if (species != "Human" && species != "Dwarf")
            return;

        if (!TryComp<BloodstreamComponent>(target, out var victimBlood))
            return;

        var bloodId = victimBlood.BloodReagent;
        if (bloodId == "AriralBlood")
        {
            _popup.PopupEntity(Loc.GetString("arkana-drink-fail-ariral"), target, user);
            return;
        }

        if (!_solutions.ResolveSolution(target, victimBlood.BloodSolutionName, ref victimBlood.BloodSolution, out var bloodSolution))
            return;

        var take = FixedPoint2.Min(DrainAmount, bloodSolution.Volume);
        if (take <= FixedPoint2.Zero)
            return;

        _ = _solutions.SplitSolution(victimBlood.BloodSolution.Value, take);
        var protein = take / FixedPoint2.New(2);
        var saline = take - protein; // ensure full conservation
        var sol = new Solution();
        sol.AddReagent("Protein", protein);
        sol.AddReagent("Saline", saline);

        if (!TryComp<BodyComponent>(user, out var body) || !_body.TryGetBodyOrganEntityComps<StomachComponent>((user, body), out var stomachs))
            return;

        foreach (var ent in stomachs)
        {
            if (_stomach.CanTransferSolution(ent.Owner, sol, ent.Comp1))
            {
                _stomach.TryTransferSolution(ent.Owner, sol, ent.Comp1);
                ev.Handled = true;
                _popup.PopupEntity(Loc.GetString("arkana-drink-success"), user, user);
                return;
            }
        }
    }

    [Dependency] private readonly BodySystem _body = default!;
}
