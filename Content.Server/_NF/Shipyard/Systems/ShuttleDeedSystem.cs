// SPDX-FileCopyrightText: 2024 Alice "Arimah" Heurlin
// SPDX-FileCopyrightText: 2025 Dvir
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Examine;
using Content.Server._NF.Shipyard.Systems;
using Content.Shared._Mono.Ships.Components;

namespace Content.Shared._NF.Shipyard;

public sealed partial class ShuttleDeedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShuttleDeedComponent, ExaminedEvent>(OnExamined);
        // When a grid/entity spawns with ShuttleDeed via prototypes/addComponents,
        // ensure the deed points to that entity as its shuttle.
        SubscribeLocalEvent<ShuttleDeedComponent, MapInitEvent>(OnMapInit);
    }

    public bool HasOwner(Entity<VesselComponent?> vessel)
    {
        return TryComp<ShuttleDeedComponent>(vessel, out var deed) && deed.DeedHolder != null;
    }

    private void OnExamined(Entity<ShuttleDeedComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        if (!string.IsNullOrEmpty(comp.ShuttleName))
        {
            var fullName = ShipyardSystem.GetFullName(comp);
            args.PushMarkup(Loc.GetString("shuttle-deed-examine-text", ("shipname", fullName)));
        }
    }

    private void OnMapInit(Entity<ShuttleDeedComponent> ent, ref MapInitEvent args)
    {
        // If the deed doesn't have a target yet, bind it to the entity itself (e.g., the grid).
        if (ent.Comp.ShuttleUid == null)
        {
            ent.Comp.ShuttleUid = ent;
            Dirty(ent);
        }
    }
}
