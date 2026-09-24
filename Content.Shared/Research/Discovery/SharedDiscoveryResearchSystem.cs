using System.Linq;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Research.Discovery;

public abstract class SharedDiscoveryResearchSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchDataDiskComponent, ComponentStartup>(OnDiskStartup);
    }

    private void OnDiskStartup(EntityUid uid, ResearchDataDiskComponent disk, ComponentStartup args)
    {
        UpdateDiskAppearance(uid, disk);
    }

    public void UpdateDiskAppearance(EntityUid uid, ResearchDataDiskComponent disk)
    {
        Appearance.SetData(uid, ResearchDataDiskVisuals.Stage, disk.Stage);
    }

    public int CountWorkingComputeOnGrid(EntityUid machine)
    {
        if (!TryComp(machine, out TransformComponent? xform) || xform.GridUid == null)
            return 0;

        var grid = xform.GridUid.Value;
        var count = 0;
        var query = EntityQueryEnumerator<ResearchComputeServerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var compute, out var tx))
        {
            if (compute.Broken)
                continue;
            if (compute.ThermalOffline)
                continue;
            if (tx.GridUid != grid)
                continue;
            count++;
        }
        return count;
    }

    public int CountLinkedComputeOnGrid(EntityUid machine)
    {
        if (!TryComp(machine, out TransformComponent? xform) || xform.GridUid == null)
            return 0;

        var grid = xform.GridUid.Value;
        var count = 0;
        var query = EntityQueryEnumerator<ResearchComputeServerComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var tx))
        {
            if (tx.GridUid == grid)
                count++;
        }
        return count;
    }

    public float ProgressDurationSeconds(int unlockedCount, float baseSeconds)
    {
        return baseSeconds + unlockedCount * DiscoveryResearchConstants.SecondsPerUnlockedTech;
    }

    public float ApplyComputeSpeed(float baseSeconds, float effectiveCompute)
    {
        if (effectiveCompute <= 0.001f)
            return baseSeconds * DiscoveryResearchConstants.NoComputePenalty;

        var fullUnits = MathF.Floor(effectiveCompute);
        var frac = effectiveCompute - fullUnits;

        float DurationAt(int n)
        {
            if (n <= 0)
                return baseSeconds * DiscoveryResearchConstants.NoComputePenalty;

            var duration = baseSeconds * DiscoveryResearchConstants.FirstComputeSpeedFactor;
            for (var i = 1; i < n; i++)
                duration *= DiscoveryResearchConstants.AdditionalComputeDiminishFactor;

            return MathF.Max(5f, duration);
        }

        if (frac <= 0.001f)
            return DurationAt((int)fullUnits);

        return MathHelper.Lerp(DurationAt((int)fullUnits), DurationAt((int)fullUnits + 1), frac);
    }

    public float DecodeDurationSeconds(int cost, int unlockedCount, float effectiveCompute)
    {
        var baseSec = cost / 1000f * DiscoveryResearchConstants.DecodeSecondsPerThousandCost;
        baseSec = ProgressDurationSeconds(unlockedCount, baseSec);
        return ApplyComputeSpeed(baseSec, effectiveCompute);
    }

    public float ScanDurationSeconds(int unlockedCount, float effectiveCompute)
    {
        var baseSec = ProgressDurationSeconds(unlockedCount, DiscoveryResearchConstants.BaseScanSeconds);
        return ApplyComputeSpeed(baseSec, effectiveCompute);
    }

    public float AutoScanDurationSeconds(int unlockedCount)
    {
        return ScanDurationSeconds(unlockedCount, 0f) * DiscoveryResearchConstants.AutoScanNoComputeMultiplier;
    }

    public void EnsureSourceYield(Entity<ResearchSourceComponent> source)
    {
        if (source.Comp.TechYieldRemaining >= 0)
            return;

        var total = RollTechYield();
        source.Comp.TechYieldTotal = total;
        source.Comp.TechYieldRemaining = total;
        Dirty(source);
    }

    private int RollTechYield()
    {
        var min = DiscoveryResearchConstants.MinTechYield;
        var max = DiscoveryResearchConstants.MaxTechYield;
        var totalWeight = 0;
        for (var count = min; count <= max; count++)
            totalWeight += TechYieldWeight(count);

        var roll = Random.Next(totalWeight);
        var cursor = 0;
        for (var count = min; count <= max; count++)
        {
            cursor += TechYieldWeight(count);
            if (roll < cursor)
                return count;
        }

        return min;
    }

    private static int TechYieldWeight(int count)
    {
        var stepsAboveMin = count - DiscoveryResearchConstants.MinTechYield;
        var weight = 1;
        for (var i = stepsAboveMin; i < DiscoveryResearchConstants.MaxTechYield - DiscoveryResearchConstants.MinTechYield; i++)
            weight *= DiscoveryResearchConstants.TechYieldWeightHalving;
        return weight;
    }

    public bool TryRollTechnology(
        ProtoId<RndFactionPrototype>? faction,
        HashSet<ProtoId<TechnologyPrototype>> exclude,
        out ProtoId<TechnologyPrototype> techId)
    {
        techId = default;
        FactionResearchProfilePrototype? profile = null;
        if (faction != null && PrototypeManager.TryIndex(faction.Value, out RndFactionPrototype? _))
            PrototypeManager.TryIndex(new ProtoId<FactionResearchProfilePrototype>(faction.Value), out profile);

        var candidates = new List<(ProtoId<TechnologyPrototype> Id, float Weight)>();
        foreach (var tech in PrototypeManager.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (tech.Hidden)
                continue;
            if (exclude.Contains(tech.ID))
                continue;

            if (faction != null)
            {
                if (tech.SignatureFor is { } signature && signature != faction)
                    continue;
                if (tech.Factions.Count > 0 && !tech.Factions.Contains(faction.Value))
                    continue;
            }

            var weight = MathF.Max(0.0001f, tech.DiscoveryWeight);

            if (profile != null)
            {
                if (profile.DisciplineBias.TryGetValue(tech.Discipline, out var discBias))
                    weight *= discBias;

                foreach (var tag in tech.Tags)
                {
                    if (profile.AffinityTags.TryGetValue(tag, out var tagBias))
                        weight *= tagBias;
                }

                if (profile.SignatureTechs.Contains(tech.ID) || tech.SignatureFor == faction)
                    weight *= profile.SignatureWeightBonus;
            }

            candidates.Add((tech.ID, weight));
        }

        if (candidates.Count == 0)
            return false;

        var total = candidates.Sum(c => c.Weight);
        var roll = Random.NextFloat(0f, total);
        var acc = 0f;
        foreach (var (id, w) in candidates)
        {
            acc += w;
            if (roll <= acc)
            {
                techId = id;
                return true;
            }
        }

        techId = candidates[^1].Id;
        return true;
    }

    public void WriteRawPreview(EntityUid uid, ResearchDataDiskComponent disk, TechnologyPrototype tech)
    {
        disk.Stage = ResearchDataDiskStage.Raw;
        disk.LockedTechId = tech.ID;
        disk.LeadDiscipline = tech.Discipline;
        disk.LeadTierHint = 0;
        disk.LeadTags = tech.Tags.ToList();
        disk.LeadRisk = tech.Cost >= 15000 ? "high" : tech.Cost >= 5000 ? "medium" : "low";
        var jitter = 1f + (Random.NextFloat() - 0.5f) * 0.4f;
        disk.LeadCostEstimate = Math.Max(100, (int)(tech.Cost * jitter));
        disk.DecodeProgress = 0;
        disk.DecodeCost = tech.Cost;
        Dirty(uid, disk);
        UpdateDiskAppearance(uid, disk);
    }

    public void ClearDisk(EntityUid uid, ResearchDataDiskComponent disk)
    {
        disk.Stage = ResearchDataDiskStage.Empty;
        disk.LockedTechId = null;
        disk.LeadDiscipline = null;
        disk.LeadTierHint = 0;
        disk.LeadTags.Clear();
        disk.LeadRisk = string.Empty;
        disk.LeadCostEstimate = 0;
        disk.DecodeProgress = 0;
        disk.DecodeCost = 0;
        Dirty(uid, disk);
        UpdateDiskAppearance(uid, disk);
    }
}
