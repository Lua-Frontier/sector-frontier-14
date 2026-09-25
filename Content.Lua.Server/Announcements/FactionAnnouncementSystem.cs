using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Lua.Shared.Announcements;
using Content.Lua.Shared.Sectors;
using Content.Server.Chat.Systems;
using Content.Shared._Mono.Company;
using Content.Shared.Administration;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server.Announcements;

public sealed class FactionAnnouncementSystem : EntitySystem, IFactionAnnouncementSystem
{
    public const string AllSectorsId = FactionAnnouncementIds.AllSectorsId;
    public const string DefaultFactionId = FactionAnnouncementIds.DefaultFactionId;
    public const string DefaultSoundPath = ChatSystem.DefaultAnnouncementSound;

    private static readonly string[] PreferredFactionOrder =
    [
        "Nanotrasen",
        "Ussp",
        "Syndicate",
        "Neutral",
        "Pirates",
        "LuaTech",
    ];

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly ISectorSystem _sectors = default!;

    public IReadOnlyList<AdminAnnounceFactionInfo> GetFactions()
    {
        var byId = new Dictionary<string, AdminAnnounceFactionInfo>();
        foreach (var company in _protos.EnumeratePrototypes<CompanyPrototype>())
        {
            if (string.IsNullOrWhiteSpace(company.AnnouncementTitle))
                continue;

            byId[company.ID] = new AdminAnnounceFactionInfo
            {
                Id = company.ID,
                Title = Loc.TryGetString(company.AnnouncementTitle, out var title)
                    ? title
                    : company.AnnouncementTitle,
                Color = company.AnnouncementColor ?? company.Color,
            };
        }

        var ordered = new List<AdminAnnounceFactionInfo>();
        foreach (var id in PreferredFactionOrder)
        {
            if (byId.Remove(id, out var info))
                ordered.Add(info);
        }

        ordered.AddRange(byId.Values.OrderBy(f => f.Title, StringComparer.CurrentCultureIgnoreCase));
        return ordered;
    }

    public IReadOnlyList<AdminAnnounceSectorInfo> GetSectors()
    {
        var sectors = new List<AdminAnnounceSectorInfo>
        {
            new()
            {
                Id = AllSectorsId,
                Name = Loc.GetString("admin-announce-sector-all"),
            }
        };

        foreach (var (id, mapId, _) in _sectors.EnumerateSectorMaps().OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase))
        {
            sectors.Add(new AdminAnnounceSectorInfo
            {
                Id = id,
                Name = _sectors.GetSectorDisplayName(mapId),
            });
        }

        return sectors;
    }

    public bool TryGetFactionIdentity(
        string factionId,
        [NotNullWhen(true)] out string? title,
        out Color color,
        [NotNullWhen(true)] out SoundSpecifier? sound)
    {
        title = null;
        color = Color.Gold;
        sound = null;

        if (!_protos.TryIndex<CompanyPrototype>(factionId, out var company) ||
            string.IsNullOrWhiteSpace(company.AnnouncementTitle))
            return false;

        title = Loc.TryGetString(company.AnnouncementTitle, out var localized)
            ? localized
            : company.AnnouncementTitle;
        color = company.AnnouncementColor ?? company.Color;
        sound = company.AnnouncementSound ?? new SoundPathSpecifier(DefaultSoundPath);
        return true;
    }

    public bool TryAnnounce(
        string message,
        string factionId,
        string sectorId,
        SoundSpecifier? soundOverride = null,
        Color? colorOverride = null)
    {
        if (!TryGetFactionIdentity(factionId, out var title, out var color, out var sound))
            return false;

        var finalColor = colorOverride ?? color;
        var finalSound = soundOverride ?? sound;

        if (string.IsNullOrWhiteSpace(sectorId) || sectorId == AllSectorsId)
        {
            _chat.DispatchGlobalAnnouncement(message, title, true, finalSound, finalColor, factionId: factionId);
            return true;
        }

        if (!_sectors.TryGetMapId(sectorId, out var mapId) || mapId == MapId.Nullspace)
            return false;

        _chat.DispatchMapAnnouncement(mapId, message, title, true, finalSound, finalColor, factionId: factionId);
        return true;
    }
}
