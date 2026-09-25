// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameObjects;

namespace Content.Lua.UIKit.Announce;

public interface IAnnouncementPortraitSystem : IEntitySystem
{
    EntityUid? CreateHologramPortrait(EntityUid speaker);
    EntityUid? CreateHologramFlag(string rsiPath, string rsiState);
    void ReleaseHologram(EntityUid? hologram);
}
