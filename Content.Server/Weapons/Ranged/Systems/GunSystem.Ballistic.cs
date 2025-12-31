// SPDX-FileCopyrightText: 2022 Kara
// SPDX-FileCopyrightText: 2022 metalgearsloth
// SPDX-FileCopyrightText: 2023 DrSmugleaf
// SPDX-FileCopyrightText: 2023 TemporalOroboros
// SPDX-FileCopyrightText: 2025 ark1368
// SPDX-FileCopyrightText: 2025 tonotom
// SPDX-FileCopyrightText: 2025 tonotom1
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    /// <summary>
    /// Adds ammo to a ballistic ammo provider by incrementing UnspawnedCount.
    /// </summary>
    public void AddBallisticAmmo(EntityUid uid, BallisticAmmoProviderComponent component, int amount = 1)
    {
        component.UnspawnedCount += amount;

        DirtyField(uid, component, nameof(BallisticAmmoProviderComponent.UnspawnedCount));
    }

    protected override void Cycle(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates)
    {
        EntityUid? ent = null;

        // TODO: Combine with TakeAmmo
        if (component.Entities.Count > 0)
        {
            var existing = component.Entities[^1];
            component.Entities.RemoveAt(component.Entities.Count - 1);
            DirtyField(uid, component, nameof(BallisticAmmoProviderComponent.Entities));

            Containers.Remove(existing, component.Container);
            EnsureShootable(existing);
        }
        else if (component.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            DirtyField(uid, component, nameof(BallisticAmmoProviderComponent.UnspawnedCount));
            ent = Spawn(component.Proto, coordinates);
            EnsureShootable(ent.Value);
        }

        if (ent != null)
            EjectCartridge(ent.Value);

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(uid, ref cycledEvent);
    }
}
