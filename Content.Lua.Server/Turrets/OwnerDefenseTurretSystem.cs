using Content.Server.Engineering.Components;
using Content.Server.Popups;
using Content.Lua.Shared.Turrets;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server.Turrets;

public sealed class OwnerDefenseTurretSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OwnerDefenseTurretComponent, SpawnAfterInteractSpawnedEvent>(OnSpawned);
        SubscribeLocalEvent<OwnerDefenseTurretComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnSpawned(Entity<OwnerDefenseTurretComponent> ent, ref SpawnAfterInteractSpawnedEvent args)
    {
        Claim(ent, args.User);
    }

    private void Claim(Entity<OwnerDefenseTurretComponent> ent, EntityUid installer)
    {
        ent.Comp.Owner = installer;
        ent.Comp.OwnerFactions.Clear();

        if (TryComp<NpcFactionMemberComponent>(installer, out var member))
        {
            foreach (var faction in member.Factions)
                ent.Comp.OwnerFactions.Add(faction);
        }

        ent.Comp.Mode = OwnerDefenseTurretMode.Faction;
        ApplyMode(ent);
    }

    private void OnGetVerbs(Entity<OwnerDefenseTurretComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || args.Hands == null)
            return;

        if (ent.Comp.Owner == null || args.User != ent.Comp.Owner.Value)
            return;

        args.Verbs.Add(CreateModeVerb(ent, args.User, OwnerDefenseTurretMode.Faction));
        args.Verbs.Add(CreateModeVerb(ent, args.User, OwnerDefenseTurretMode.Owner));
    }

    private Verb CreateModeVerb(Entity<OwnerDefenseTurretComponent> ent, EntityUid user, OwnerDefenseTurretMode mode)
    {
        var captured = mode;
        return new Verb
        {
            Text = Loc.GetString(mode == OwnerDefenseTurretMode.Faction
                ? "owner-defense-turret-mode-faction"
                : "owner-defense-turret-mode-owner"),
            Category = VerbCategory.DefenseMode,
            Disabled = ent.Comp.Mode == mode,
            Priority = mode == OwnerDefenseTurretMode.Faction ? 1 : 0,
            Act = () =>
            {
                if (!Exists(ent) || !TryComp(ent, out OwnerDefenseTurretComponent? comp))
                    return;

                if (comp.Owner != user)
                    return;

                comp.Mode = captured;
                ApplyMode((ent, comp));
                _popup.PopupEntity(
                    Loc.GetString("owner-defense-turret-mode-set",
                        ("mode", Loc.GetString(captured == OwnerDefenseTurretMode.Faction
                            ? "owner-defense-turret-mode-faction"
                            : "owner-defense-turret-mode-owner"))),
                    ent,
                    user);
            },
        };
    }

    private void ApplyMode(Entity<OwnerDefenseTurretComponent> ent)
    {
        RemComp<FactionExceptionComponent>(ent);

        switch (ent.Comp.Mode)
        {
            case OwnerDefenseTurretMode.Owner:
                ApplyOwnerMode(ent);
                break;
            case OwnerDefenseTurretMode.Faction:
            default:
                ApplyFactionMode(ent);
                break;
        }

        ApplyOwnerAllies(ent);
    }

    private void ApplyOwnerAllies(Entity<OwnerDefenseTurretComponent> ent)
    {
        if (ent.Comp.Owner is not { } owner || !Exists(owner))
            return;

        _faction.IgnoreEntity(ent.Owner, owner);

        var query = EntityQueryEnumerator<OwnerDefenseTurretComponent>();
        while (query.MoveNext(out var otherUid, out var other))
        {
            if (otherUid == ent.Owner)
                continue;

            if (other.Owner != owner)
                continue;

            _faction.IgnoreEntity(ent.Owner, otherUid);
            _faction.IgnoreEntity(otherUid, ent.Owner);
        }
    }

    private void ApplyFactionMode(Entity<OwnerDefenseTurretComponent> ent)
    {
        _faction.ClearFactions(ent.Owner);

        if (ent.Comp.OwnerFactions.Count == 0)
        {
            ApplyOwnerMode(ent);
            return;
        }

        _faction.AddFactions(ent.Owner, new HashSet<ProtoId<NpcFactionPrototype>>(ent.Comp.OwnerFactions));
    }

    private void ApplyOwnerMode(Entity<OwnerDefenseTurretComponent> ent)
    {
        _faction.ClearFactions(ent.Owner);
        _faction.AddFaction(ent.Owner, ent.Comp.OwnerOnlyFaction);
    }
}
