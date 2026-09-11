// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Holopad;

namespace Content.Client.Holopad;

public sealed partial class HolopadSystem
{
    public void RefreshHologram(EntityUid hologram, EntityUid? linkedEntity)
    {
        if (!TryComp<HolopadHologramComponent>(hologram, out var holopadHologram))
            return;

        holopadHologram.LinkedEntity = linkedEntity;
        UpdateHologramSprite(hologram, linkedEntity);
    }
}
