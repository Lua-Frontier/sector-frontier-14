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
}
