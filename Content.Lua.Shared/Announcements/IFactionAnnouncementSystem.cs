using System.Collections.Generic;
using Content.Shared.Administration;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Lua.Shared.Announcements;

public static class FactionAnnouncementIds
{
    public const string AllSectorsId = "all";
    public const string DefaultFactionId = "Nanotrasen";
}

public interface IFactionAnnouncementSystem : IEntitySystem
{
    IReadOnlyList<AdminAnnounceFactionInfo> GetFactions();
    IReadOnlyList<AdminAnnounceSectorInfo> GetSectors();
    bool TryAnnounce(string message, string factionId, string sectorId, SoundSpecifier? soundOverride = null, Color? colorOverride = null);
}
