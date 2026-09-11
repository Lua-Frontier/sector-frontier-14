// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Holopad;
using Content.Shared.Holopad;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Lua.Announce;

public sealed class AnnouncementPortraitSystem : EntitySystem
{
    [Dependency] private readonly HolopadSystem _holopad = default!;

    public EntityUid? CreateHologramPortrait(EntityUid speaker)
    {
        if (!Exists(speaker))
            return null;

        var hologram = Spawn("HolopadHologram");
        _holopad.RefreshHologram(hologram, speaker);
        return hologram;
    }

    public EntityUid? CreateHologramFlag(string rsiPath, string rsiState)
    {
        if (string.IsNullOrWhiteSpace(rsiPath) || string.IsNullOrWhiteSpace(rsiState))
            return null;

        var hologram = Spawn("HolopadHologram");
        var holo = Comp<HolopadHologramComponent>(hologram);
        holo.LinkedEntity = null;
        holo.RsiPath = rsiPath;
        holo.RsiState = rsiState;

        _holopad.RefreshHologram(hologram, null);
        return hologram;
    }

    public void ReleaseHologram(EntityUid? hologram)
    {
        if (hologram == null || !Exists(hologram))
            return;

        QueueDel(hologram.Value);
    }
}
