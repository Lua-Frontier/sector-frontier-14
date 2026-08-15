using Content.Shared._Mono.Company;
using Content.Shared.CrewManifest;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CrewManifest.UI;

public sealed class CrewManifestListing : BoxContainer
{
    [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    private readonly SpriteSystem _spriteSystem;

    public CrewManifestListing()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entitySystem.GetEntitySystem<SpriteSystem>();
    }

    public void AddCrewManifestEntries(CrewManifestEntries entries)
    {
        if (entries.GroupByCompany)
        {
            AddCompanyGroupedEntries(entries);
            return;
        }

        var entryDict = new Dictionary<DepartmentPrototype, List<CrewManifestEntry>>();

        foreach (var entry in entries.Entries)
        {
            foreach (var department in _prototypeManager.EnumeratePrototypes<DepartmentPrototype>())
            {
                // this is a little expensive, and could be better
                if (department.Roles.Contains(entry.JobPrototype))
                {
                    entryDict.GetOrNew(department).Add(entry);
                }
            }
        }

        var entryList = new List<(DepartmentPrototype section, List<CrewManifestEntry> entries)>();

        foreach (var (section, listing) in entryDict)
        {
            listing.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            entryList.Add((section, listing));
        }

        entryList.Sort((a, b) => DepartmentUIComparer.Instance.Compare(a.section, b.section));

        foreach (var item in entryList)
        {
            AddChild(new CrewManifestSection(_prototypeManager, _spriteSystem, item.section, item.entries));
        }
    }

    private void AddCompanyGroupedEntries(CrewManifestEntries entries)
    {
        var byCompany = new Dictionary<string, List<CrewManifestEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.Entries)
        {
            var companyId = string.IsNullOrWhiteSpace(entry.CompanyId) ? "None" : entry.CompanyId;
            byCompany.GetOrNew(companyId).Add(entry);
        }

        var sections = new List<(string companyId, string title, List<CrewManifestEntry> listing)>();

        foreach (var (companyId, listing) in byCompany)
        {
            listing.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

            var title = companyId;
            if (_prototypeManager.TryIndex<CompanyPrototype>(companyId, out var proto))
                title = proto.Name;

            sections.Add((companyId, title, listing));
        }

        sections.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.CurrentCultureIgnoreCase));

        foreach (var section in sections)
        {
            AddChild(new CrewManifestSection(
                _prototypeManager,
                _spriteSystem,
                section.title,
                section.listing,
                showPlayerNames: section.listing.Exists(e => !string.IsNullOrWhiteSpace(e.PlayerName))));
        }
    }
}
