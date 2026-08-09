// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Company.Components;
using Content.Server._Mono.Company;
using Content.Server._NF.Station.Components;
using Content.Server.Station.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._Mono.Company;
using Content.Shared._Mono.Radar;
using Content.Shared.Roles;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server._Lua.Company;

public sealed class FactionOwnedStationSystem : EntitySystem
{
    private const int CapturedStationBonusSlots = 3;
    private static readonly Dictionary<string, string> BaseFactionJobs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pirates"] = "NFPirate",
        ["Security"] = "Cadet",
        ["Neutral"] = "Contractor",
        ["Nanotrasen"] = "Pilot",
        ["Syndicate"] = "OutpostTypanResearcher",
        ["StormCreed"] = "StormCreedSoldier",
    };

    [Dependency] private readonly CompanySystem _company = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly Content.Server.Station.Systems.StationSystem _station = default!;
    [Dependency] private readonly Content.Server.Station.Systems.StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExtraStationInformationComponent, ComponentStartup>(OnExtraStationStartup);
        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit);
        SubscribeLocalEvent<FactionOwnedStationComponent, StationGridAddedEvent>(OnStationGridAdded);
    }

    public bool TryGetCurrentOwner(EntityUid station, out string? companyId, FactionOwnedStationComponent? ownedStation = null)
    {
        companyId = null;
        if (!Resolve(station, ref ownedStation, false))
            return false;

        companyId = NormalizeCompanyId(ownedStation.CurrentCompany);
        return !string.IsNullOrWhiteSpace(companyId);
    }

    public bool TryGetOriginalOwner(EntityUid station, out string? companyId, FactionOwnedStationComponent? ownedStation = null)
    {
        companyId = null;
        if (!Resolve(station, ref ownedStation, false))
            return false;

        companyId = NormalizeCompanyId(ownedStation.OriginalCompany);
        return !string.IsNullOrWhiteSpace(companyId);
    }

    public string? GetSpawnAccessCompanies(EntityUid station)
    {
        var companies = new List<string>();
        TryComp<ExtraStationInformationComponent>(station, out var extraStationInformation);

        if (TryGetCurrentOwner(station, out var owner) && !string.IsNullOrWhiteSpace(owner))
            AddCompanyIds(companies, owner);
        else if (extraStationInformation != null)
            AddCompanyIds(companies, extraStationInformation.RequiredCompany);

        if (extraStationInformation != null)
            AddCompanyIds(companies, extraStationInformation.AdditionalSpawnCompanies);

        return companies.Count == 0 ? null : string.Join(", ", companies);
    }

    public void SetOwner(EntityUid station, string? companyId, FactionOwnedStationComponent? ownedStation = null)
    {
        ownedStation ??= EnsureComp<FactionOwnedStationComponent>(station);
        var previousOwner = NormalizeCompanyId(ownedStation.CurrentCompany);
        var normalizedCompanyId = NormalizeCompanyId(companyId);
        if (string.Equals(previousOwner, normalizedCompanyId, StringComparison.OrdinalIgnoreCase))
            return;

        ownedStation.CurrentCompany = normalizedCompanyId;
        RebuildStationJobsForOwner(station, normalizedCompanyId, ownedStation);
        RebuildMainBaseJobsForCompany(previousOwner);
        RebuildMainBaseJobsForCompany(normalizedCompanyId);
        RefreshStationOwnershipDisplay(station, ownedStation);
        _stationJobs.UpdateJobsAvailable();
    }

    public int CountOwnedStations(string companyId)
    {
        var normalized = NormalizeCompanyId(companyId);
        if (normalized == null)
            return 0;

        var count = 0;
        var query = EntityQueryEnumerator<FactionOwnedStationComponent>();
        while (query.MoveNext(out _, out var ownedStation))
        {
            if (!string.Equals(NormalizeCompanyId(ownedStation.CurrentCompany), normalized, StringComparison.OrdinalIgnoreCase))
                continue;

            count++;
        }

        return count;
    }
    public void BuildMapOwnership(Dictionary<MapId, string> ownerByMap, Dictionary<MapId, string> colorHexByMap)
    {
        ownerByMap.Clear();
        colorHexByMap.Clear();

        var query = EntityQueryEnumerator<FactionOwnedStationComponent, StationDataComponent>();
        while (query.MoveNext(out var station, out var ownedStation, out var stationData))
        {
            var owner = NormalizeCompanyId(ownedStation.CurrentCompany);
            if (owner == null)
                continue;

            MapId? mapId = null;
            foreach (var grid in stationData.Grids)
            {
                if (TerminatingOrDeleted(grid))
                    continue;

                var gridMap = Transform(grid).MapID;
                if (gridMap == MapId.Nullspace)
                    continue;

                mapId = gridMap;
                break;
            }

            if (mapId == null)
                continue;

            ownerByMap[mapId.Value] = owner;
            colorHexByMap[mapId.Value] = GetOwnerColor(owner).ToHex();
        }
    }

    private void OnExtraStationStartup(EntityUid uid, ExtraStationInformationComponent component, ComponentStartup args)
    {
        var ownedStation = EnsureComp<FactionOwnedStationComponent>(uid);
        var requiredCompany = NormalizeStartupOwner(component.RequiredCompany);

        if (string.IsNullOrWhiteSpace(ownedStation.OriginalCompany))
            ownedStation.OriginalCompany = requiredCompany;

        if (string.IsNullOrWhiteSpace(ownedStation.CurrentCompany))
            ownedStation.CurrentCompany = requiredCompany;
    }

    private void OnStationPostInit(ref StationPostInitEvent args)
    {
        if (!TryComp<FactionOwnedStationComponent>(args.Station.Owner, out var ownedStation))
            return;

        RefreshStationOwnershipDisplay(args.Station.Owner, ownedStation);
    }

    private void OnStationGridAdded(EntityUid uid, FactionOwnedStationComponent component, ref StationGridAddedEvent args)
    {
        RefreshStationOwnershipDisplay(uid, component);
    }

    private void RefreshStationOwnershipDisplay(EntityUid station, FactionOwnedStationComponent ownedStation)
    {
        if (!TryComp<Content.Server.Station.Components.StationDataComponent>(station, out var stationData))
            return;

        var baseName = EnsureBaseStationName(station, stationData, ownedStation);
        var displayName = BuildDisplayName(baseName, ownedStation.CurrentCompany);
        var iffColor = GetOwnerColor(ownedStation.CurrentCompany);
        var companyName = ownedStation.CurrentCompany ?? "None";

        foreach (var grid in stationData.Grids)
        {
            if (TerminatingOrDeleted(grid))
                continue;

            _company.SetCompany(grid, companyName);
            SyncGridRadarDisplay(grid, iffColor);
        }

        var largestGrid = _station.GetLargestGrid(stationData);
        if (largestGrid == null || TerminatingOrDeleted(largestGrid.Value))
            return;

        _metaData.SetEntityName(largestGrid.Value, displayName);
    }

    private string EnsureBaseStationName(EntityUid station, Content.Server.Station.Components.StationDataComponent stationData, FactionOwnedStationComponent ownedStation)
    {
        if (!string.IsNullOrWhiteSpace(ownedStation.OriginalStationName))
            return ownedStation.OriginalStationName;

        var largestGrid = _station.GetLargestGrid(stationData);
        var baseName = largestGrid != null
            ? MetaData(largestGrid.Value).EntityName
            : string.Empty;

        if (string.IsNullOrWhiteSpace(baseName))
            return Name(station);

        ownedStation.OriginalStationName = baseName;
        return ownedStation.OriginalStationName;
    }

    private string BuildDisplayName(string baseName, string? companyId)
    {
        return baseName;
    }

    private void SyncGridRadarDisplay(EntityUid grid, Color color, bool updateIff = true)
    {
        if (updateIff)
            _shuttle.ForceSetIFFColor(grid, color);

        var blip = EnsureComp<RadarBlipComponent>(grid);
        var changed = false;

        if (blip.RadarColor != color)
        {
            blip.RadarColor = color;
            changed = true;
        }

        if (blip.HighlightedRadarColor != color)
        {
            blip.HighlightedRadarColor = color;
            changed = true;
        }

        if (!blip.Enabled)
        {
            blip.Enabled = true;
            changed = true;
        }

        if (!blip.VisibleFromOtherGrids)
        {
            blip.VisibleFromOtherGrids = true;
            changed = true;
        }

        if (blip.RequireNoGrid)
        {
            blip.RequireNoGrid = false;
            changed = true;
        }

        if (blip.MaxDistance < 4096f)
        {
            blip.MaxDistance = 4096f;
            changed = true;
        }

        if (changed)
            Dirty(grid, blip);
    }

    private Color GetOwnerColor(string? companyId)
    {
        if (!string.IsNullOrWhiteSpace(companyId) && _prototypes.TryIndex<CompanyPrototype>(companyId, out var prototype))
            return prototype.Color;

        return IFFComponent.IFFColor;
    }

    private void RebuildStationJobsForOwner(EntityUid station, string? ownerCompanyId, FactionOwnedStationComponent ownedStation)
    {
        if (!TryComp<StationJobsComponent>(station, out var stationJobs))
            return;

        var occupiedCounts = new Dictionary<ProtoId<JobPrototype>, int>();
        foreach (var jobs in stationJobs.PlayerJobs.Values)
        {
            foreach (var jobId in jobs)
            {
                occupiedCounts[jobId] = occupiedCounts.GetValueOrDefault(jobId) + 1;
            }
        }

        var desiredSlots = stationJobs.SetupAvailableJobs.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value[1] < 0 ? (int?) null : kvp.Value[1]);

        if (ownedStation.DisableJobsWhenLost && IsCapturedStationOwner(ownerCompanyId, ownedStation.OriginalCompany))
        {
            foreach (var jobId in desiredSlots.Keys.ToArray())
            {
                if (!_prototypes.TryIndex<JobPrototype>(jobId, out var jobPrototype))
                    continue;

                if (string.IsNullOrWhiteSpace(jobPrototype.RequiredCompany))
                    continue;

                if (!IsMatchingCompany(ownerCompanyId, jobPrototype.RequiredCompany))
                    desiredSlots[jobId] = 0;
            }
        }

        if (ownedStation.MainBase)
            ApplyMainBaseCaptureBonus(desiredSlots, ownerCompanyId, ownedStation);

        var allJobIds = new HashSet<ProtoId<JobPrototype>>(stationJobs.JobList.Keys);
        allJobIds.UnionWith(desiredSlots.Keys);

        foreach (var jobId in allJobIds)
        {
            if (!desiredSlots.TryGetValue(jobId, out var desiredCapacity))
                desiredCapacity = 0;

            if (desiredCapacity == null)
            {
                _stationJobs.MakeJobUnlimited(station, jobId, stationJobs);
                continue;
            }

            var occupied = occupiedCounts.GetValueOrDefault(jobId);
            var remaining = Math.Max(desiredCapacity.Value - occupied, 0);
            _stationJobs.TrySetJobSlot(station, jobId, remaining, true, stationJobs);
        }
    }

    private void RebuildMainBaseJobsForCompany(string? companyId)
    {
        var normalizedCompanyId = NormalizeCompanyId(companyId);
        if (normalizedCompanyId == null)
            return;

        var query = EntityQueryEnumerator<FactionOwnedStationComponent>();
        while (query.MoveNext(out var station, out var ownedStation))
        {
            if (!ownedStation.MainBase)
                continue;

            if (!string.Equals(NormalizeCompanyId(ownedStation.CurrentCompany), normalizedCompanyId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(NormalizeCompanyId(ownedStation.OriginalCompany), normalizedCompanyId, StringComparison.OrdinalIgnoreCase))
                continue;

            RebuildStationJobsForOwner(station, normalizedCompanyId, ownedStation);
        }
    }

    private void ApplyMainBaseCaptureBonus(Dictionary<ProtoId<JobPrototype>, int?> desiredSlots, string? ownerCompanyId, FactionOwnedStationComponent ownedStation)
    {
        var bonusOwner = GetMainBaseBonusOwner(ownerCompanyId, ownedStation);
        if (bonusOwner == null || !BaseFactionJobs.TryGetValue(bonusOwner, out var baseJobId))
            return;

        var bonusSlots = CountCapturedStationsForCompany(bonusOwner) * CapturedStationBonusSlots;
        if (bonusSlots <= 0)
            return;

        var jobId = new ProtoId<JobPrototype>(baseJobId);
        if (!desiredSlots.TryGetValue(jobId, out var existingSlots))
        {
            desiredSlots[jobId] = bonusSlots;
            return;
        }

        if (existingSlots != null)
            desiredSlots[jobId] = existingSlots.Value + bonusSlots;
    }

    private int CountCapturedStationsForCompany(string companyId)
    {
        var count = 0;
        var query = EntityQueryEnumerator<FactionOwnedStationComponent>();
        while (query.MoveNext(out _, out var ownedStation))
        {
            if (!IsCapturedStationOwner(ownedStation.CurrentCompany, ownedStation.OriginalCompany))
                continue;

            if (!string.Equals(NormalizeCompanyId(ownedStation.CurrentCompany), companyId, StringComparison.OrdinalIgnoreCase))
                continue;

            count++;
        }

        return count;
    }

    private static string? GetMainBaseBonusOwner(string? ownerCompanyId, FactionOwnedStationComponent ownedStation)
    {
        var normalizedOwner = NormalizeCompanyId(ownerCompanyId);
        var normalizedOriginal = NormalizeCompanyId(ownedStation.OriginalCompany);
        if (normalizedOwner == null || normalizedOriginal == null)
            return null;

        if (!string.Equals(normalizedOwner, normalizedOriginal, StringComparison.OrdinalIgnoreCase))
            return null;

        return normalizedOwner;
    }

    private static bool IsCapturedStationOwner(string? ownerCompanyId, string? originalCompanyId)
    {
        var normalizedOwner = NormalizeCompanyId(ownerCompanyId);
        if (string.IsNullOrWhiteSpace(normalizedOwner))
            return false;

        var normalizedOriginal = NormalizeCompanyId(originalCompanyId);
        if (!string.IsNullOrWhiteSpace(normalizedOriginal) &&
            string.Equals(normalizedOwner, normalizedOriginal, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string? GetCapturedStationBonusOwner(string? ownerCompanyId, string? originalCompanyId)
    {
        if (!IsCapturedStationOwner(ownerCompanyId, originalCompanyId))
            return null;

        return NormalizeCompanyId(ownerCompanyId);
    }

    private static bool IsMatchingCompany(string? profileCompany, string? requiredCompany)
    {
        if (string.IsNullOrWhiteSpace(requiredCompany))
            return true;

        var normalizedProfile = NormalizeCompanyId(profileCompany) ?? "None";

        foreach (var companyId in requiredCompany.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalizedRequired = NormalizeCompanyId(companyId);
            if (normalizedRequired == null)
                continue;

            if (string.Equals(normalizedProfile, normalizedRequired, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? NormalizeCompanyId(string? companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return null;

        var trimmed = companyId.Trim();
        return string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }

    private static string? NormalizeStartupOwner(string? companyId)
    {
        var normalized = NormalizeCompanyId(companyId);
        return string.Equals(normalized, "Neutral", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private static void AddCompanyIds(List<string> companies, string? companyIds)
    {
        if (string.IsNullOrWhiteSpace(companyIds))
            return;

        foreach (var companyId in companyIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizeCompanyId(companyId);
            if (normalized == null)
                continue;

            if (!companies.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
                companies.Add(normalized);
        }
    }
}
