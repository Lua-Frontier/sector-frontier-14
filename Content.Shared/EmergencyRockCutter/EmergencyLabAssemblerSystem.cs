using System.Linq;
using Content.Shared._Mono.Company;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.EmergencyRockCutter;

public sealed class EmergencyLabAssemblerSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;

    private static readonly Dictionary<string, ProtoId<RndFactionPrototype>> CompanyFactionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Nanotrasen"] = "Nanotrasen",
        ["Syndicate"] = "Syndicate",
        ["Pirates"] = "Pirates",
        ["Ussp"] = "Ussp",
        ["Security"] = "Security",
        ["Neutral"] = "Neutral",
        ["LuaTech"] = "LuaTech",
        ["Mercenary"] = "Neutral",
        ["Mercenaries"] = "Neutral",
    };

    private static readonly Dictionary<string, ProtoId<RndFactionPrototype>> NpcFactionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NanoTrasen"] = "Nanotrasen",
        ["Nanotrasen"] = "Nanotrasen",
        ["Syndicate"] = "Syndicate",
        ["Pirate"] = "Pirates",
        ["Pirates"] = "Pirates",
        ["Ussp"] = "Ussp",
        ["USSP"] = "Ussp",
        ["Security"] = "Security",
        ["Nfsd"] = "Security",
        ["NFSD"] = "Security",
        ["TSFMC"] = "Security",
        ["Mercenary"] = "Neutral",
        ["Neutral"] = "Neutral",
        ["LuaTech"] = "LuaTech",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmergencyLabAssemblerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<EmergencyLabAssemblerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EmergencyLabAssemblerComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<EmergencyLabAssemblerComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(RCDSystem)]);
        SubscribeLocalEvent<RCDOperationCompletedEvent>(OnRcdCompleted);
    }

    private void OnAfterInteract(Entity<EmergencyLabAssemblerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;
        if (TryComp<ItemToggleComponent>(ent, out var toggle) && toggle.Activated)
            return;
        if (HasComp<RCDComponent>(ent))
            args.Handled = true;
    }

    private void OnActivateAttempt(Entity<EmergencyLabAssemblerComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!ent.Comp.KitInitialized || ent.Comp.RemainingPrototypes.Count > 0)
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString("emergency-lab-assembler-spent");
    }

    private void OnExamined(Entity<EmergencyLabAssemblerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var labOn = TryComp<ItemToggleComponent>(ent, out var toggle) && toggle.Activated;
        if (labOn)
        {
            args.PushMarkup(Loc.GetString("emergency-lab-assembler-examine-lab",
                ("remaining", ent.Comp.RemainingPrototypes.Count)));
        }
        else if (ent.Comp.KitInitialized && ent.Comp.RemainingPrototypes.Count == 0)
        {
            args.PushMarkup(Loc.GetString("emergency-lab-assembler-examine-spent"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("emergency-lab-assembler-examine-cutter"));
        }
    }

    private void OnToggled(Entity<EmergencyLabAssemblerComponent> ent, ref ItemToggledEvent args)
    {
        if (args.Activated)
            EnableLabMode(ent, args.User);
        else
            DisableLabMode(ent);
    }

    private void OnRcdCompleted(ref RCDOperationCompletedEvent args)
    {
        if (!TryComp<EmergencyLabAssemblerComponent>(args.Rcd, out var lab) ||
            !TryComp<RCDComponent>(args.Rcd, out var rcd))
            return;

        var used = rcd.ProtoId;
        lab.RemainingPrototypes.Remove(used);
        Dirty(args.Rcd, lab);

        if (lab.RemainingPrototypes.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("emergency-lab-assembler-spent"), args.Rcd, args.User);
            _toggle.TryDeactivate(args.Rcd, args.User);
            return;
        }

        ApplyRcdPrototypes(args.Rcd, lab.RemainingPrototypes);

        if (args.User != default)
            _ui.TryOpenUi(args.Rcd, EmergencyLabUiKey.Key, args.User);
    }

    private void EnableLabMode(Entity<EmergencyLabAssemblerComponent> ent, EntityUid? user)
    {
        BindOrRefreshFactionKit(ent, user);

        if (ent.Comp.RemainingPrototypes.Count == 0)
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("emergency-lab-assembler-spent"), ent, user.Value);
            _toggle.TryDeactivate(ent.Owner, user);
            return;
        }
        ApplyRcdPrototypes(ent.Owner, ent.Comp.RemainingPrototypes);

        if (user != null)
        {
            _popup.PopupClient(Loc.GetString("emergency-lab-assembler-lab-on"), ent, user.Value);
            _ui.TryOpenUi(ent.Owner, EmergencyLabUiKey.Key, user.Value);
        }
    }

    private void BindOrRefreshFactionKit(Entity<EmergencyLabAssemblerComponent> ent, EntityUid? user)
    {
        var resolved = ResolveFaction(user, ent.Comp) ?? ent.Comp.DefaultFaction;

        if (!ent.Comp.KitInitialized)
        {
            ent.Comp.BoundFaction = resolved;
            ent.Comp.RemainingPrototypes = BuildInitialKit(ent.Comp);
            ent.Comp.KitInitialized = true;
            Dirty(ent);
            return;
        }

        if (ent.Comp.BoundFaction == resolved)
            return;
        if (!CanRebindFaction(ent.Comp))
            return;

        RemoveAllFactionProtos(ent.Comp);
        ent.Comp.BoundFaction = resolved;
        if (ent.Comp.FactionPrototypes.TryGetValue(resolved, out var list))
        {
            foreach (var proto in list)
                ent.Comp.RemainingPrototypes.Add(proto);
        }

        Dirty(ent);
    }

    private static bool CanRebindFaction(EmergencyLabAssemblerComponent comp)
    {
        if (comp.BoundFaction is not { } bound)
            return true;

        if (!comp.FactionPrototypes.TryGetValue(bound, out var list) || list.Count == 0)
            return true;
        return list.All(p => comp.RemainingPrototypes.Contains(p));
    }

    private static void RemoveAllFactionProtos(EmergencyLabAssemblerComponent comp)
    {
        foreach (var list in comp.FactionPrototypes.Values)
        {
            foreach (var proto in list)
                comp.RemainingPrototypes.Remove(proto);
        }
    }

    private HashSet<ProtoId<RCDPrototype>> BuildInitialKit(EmergencyLabAssemblerComponent comp)
    {
        var available = new HashSet<ProtoId<RCDPrototype>>(comp.SharedPrototypes);
        if (comp.BoundFaction is { } faction &&
            comp.FactionPrototypes.TryGetValue(faction, out var factionProtos))
        {
            foreach (var proto in factionProtos)
                available.Add(proto);
        }

        return available;
    }

    private void ApplyRcdPrototypes(EntityUid uid, HashSet<ProtoId<RCDPrototype>> available)
    {
        if (available.Count == 0)
            return;

        var first = available.First();
        if (TryComp<RCDComponent>(uid, out var existing))
        {
            existing.AvailablePrototypes = new HashSet<ProtoId<RCDPrototype>>(available);
            if (!available.Contains(existing.ProtoId))
                existing.ProtoId = first;
            Dirty(uid, existing);
            return;
        }

        AddComp(uid, new RCDComponent
        {
            AvailablePrototypes = new HashSet<ProtoId<RCDPrototype>>(available),
            ProtoId = first,
        });
    }

    private void DisableLabMode(Entity<EmergencyLabAssemblerComponent> ent)
    {
        _ui.CloseUi(ent.Owner, EmergencyLabUiKey.Key);
        RemComp<RCDComponent>(ent.Owner);
    }

    private ProtoId<RndFactionPrototype>? ResolveFaction(EntityUid? user, EmergencyLabAssemblerComponent comp)
    {
        if (user == null)
            return null;

        if (TryComp<CompanyComponent>(user.Value, out var company))
        {
            if (!string.IsNullOrWhiteSpace(company.CompanyName) &&
                !string.Equals(company.CompanyName, "None", StringComparison.OrdinalIgnoreCase))
            {
                ProtoId<RndFactionPrototype> companyId = company.CompanyName;
                if (comp.FactionPrototypes.ContainsKey(companyId))
                    return companyId;

                if (CompanyFactionMap.TryGetValue(company.CompanyName, out var fromCompany))
                    return fromCompany;
            }

            return null;
        }

        if (TryComp<NpcFactionMemberComponent>(user.Value, out var member))
        {
            foreach (var faction in member.Factions)
            {
                if (NpcFactionMap.TryGetValue(faction.Id, out var mapped))
                    return mapped;
            }
        }

        return null;
    }
}
