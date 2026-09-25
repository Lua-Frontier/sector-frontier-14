using System.Linq;
using Content.Lua.Shared.Research.Discovery;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Content.Shared.Research.Discovery;

namespace Content.Lua.Server.Research.Discovery;

public sealed partial class DiscoveryResearchSystem
{
    private void OnAnalysisUiOpen(EntityUid uid, DiscoveryAnalysisConsoleComponent comp, AfterActivatableUIOpenEvent args)
    {
        RefreshAnalysisUi(uid, comp);
    }

    private void OnAnalysisResearchRegistrationChanged(EntityUid uid, DiscoveryAnalysisConsoleComponent comp, ref ResearchRegistrationChangedEvent args)
    {
        if (args.Server == null)
            PauseAnalysisDecode(uid, comp);
        RefreshAnalysisUi(uid, comp);
    }

    private void RefreshAnalysisUi(EntityUid uid, DiscoveryAnalysisConsoleComponent comp)
    {
        _ui.SetUiState(uid, DiscoveryAnalysisUiKey.Key, BuildAnalysisState(uid, comp));
    }

    private DiscoveryAnalysisBoundUserInterfaceState BuildAnalysisState(EntityUid uid, DiscoveryAnalysisConsoleComponent comp)
    {
        var state = new DiscoveryAnalysisBoundUserInterfaceState
        {
            Decoding = comp.Decoding,
            LinkedComputeCount = CountLinkedComputeOnGrid(uid),
            WorkingComputeCount = CountPoweredWorkingCompute(uid),
        };

        var hasServer = TryGetClientServer(uid, out _, out var server, out var db);
        state.HasServer = hasServer;
        if (hasServer)
            state.ServerPoints = server.Points;

        if (!TryGetDisk(uid, comp.DiskSlotId, out _, out var disk))
            return state;

        state.HasDisk = true;
        state.DiskStage = disk.Stage;
        state.DecodeProgress = disk.DecodeProgress;
        state.DecodeCost = disk.DecodeCost;

        if (disk.LockedTechId != null &&
            PrototypeManager.TryIndex(disk.LockedTechId.Value, out TechnologyPrototype? durationTech))
        {
            var unlocked = hasServer ? db!.UnlockedTechnologies.Count : 0;
            state.DecodeDuration = DecodeDurationSeconds(durationTech.Cost, unlocked, GetEffectiveComputeLoad(uid));
        }

        if (disk.Stage == ResearchDataDiskStage.Raw)
        {
            state.PreviewDiscipline = disk.LeadDiscipline;
            state.PreviewTierHint = disk.LeadTierHint;
            state.PreviewTags = disk.LeadTags.ToList();
            state.PreviewCostEstimate = disk.LeadCostEstimate;
            state.PreviewRisk = disk.LeadRisk;
        }

        if (disk.LockedTechId != null && PrototypeManager.TryIndex(disk.LockedTechId.Value, out TechnologyPrototype? tech))
        {
            if (disk.Stage == ResearchDataDiskStage.Decoded)
            {
                state.TechId = tech.ID;
                state.TechName = TechnologyDisplayName(tech);
                state.Discipline = tech.Discipline;
                state.Cost = tech.Cost;
                state.RecipeIds = tech.RecipeUnlocks.Select(r => (string)r).ToList();
            }

            if (hasServer)
                state.AlreadyUnlocked = db!.UnlockedTechnologies.Contains(tech.ID);
        }

        return state;
    }

    private void OnStartDecode(EntityUid uid, DiscoveryAnalysisConsoleComponent comp, DiscoveryStartDecodeMessage args)
    {
        if (comp.Decoding)
            return;

        if (!TryGetClientServer(uid, out var server, out var serverComp, out _))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-server"), uid, args.Actor);
            return;
        }

        if (!TryGetDisk(uid, comp.DiskSlotId, out var diskUid, out var disk) ||
            disk.Stage != ResearchDataDiskStage.Raw ||
            disk.LockedTechId == null ||
            !PrototypeManager.TryIndex(disk.LockedTechId.Value, out TechnologyPrototype? tech))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-raw-disk"), uid, args.Actor);
            return;
        }
        if (disk.DecodeCost > 0)
        {
            comp.Decoding = true;
            Dirty(uid, comp);
            RefreshAnalysisUi(uid, comp);
            return;
        }

        if (serverComp.Points < tech.Cost)
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-points"), uid, args.Actor);
            return;
        }

        _research.ModifyServerPoints(server, -tech.Cost);
        disk.DecodeCost = tech.Cost;
        disk.DecodeProgress = 0;
        Dirty(diskUid, disk);

        comp.Decoding = true;
        Dirty(uid, comp);
        RefreshAnalysisUi(uid, comp);
    }

    private void UpdateDecodes(float frameTime)
    {
        var query = EntityQueryEnumerator<DiscoveryAnalysisConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Decoding)
                continue;

            if (!TryGetClientServer(uid, out _, out _, out var db) ||
                !TryGetDisk(uid, comp.DiskSlotId, out var diskUid, out var disk) ||
                disk.Stage != ResearchDataDiskStage.Raw ||
                disk.LockedTechId == null ||
                !PrototypeManager.TryIndex(disk.LockedTechId.Value, out TechnologyPrototype? tech))
            {
                PauseAnalysisDecode(uid, comp);
                if (_ui.IsUiOpen(uid, DiscoveryAnalysisUiKey.Key))
                    RefreshAnalysisUi(uid, comp);
                continue;
            }

            var unlocked = db.UnlockedTechnologies.Count;
            var duration = DecodeDurationSeconds(tech.Cost, unlocked, GetEffectiveComputeLoad(uid));

            disk.DecodeProgress += frameTime;
            Dirty(diskUid, disk);

            if (disk.DecodeProgress < duration)
            {
                if (_ui.IsUiOpen(uid, DiscoveryAnalysisUiKey.Key))
                    RefreshAnalysisUi(uid, comp);
                continue;
            }

            disk.Stage = ResearchDataDiskStage.Decoded;
            disk.DecodeProgress = duration;
            Dirty(diskUid, disk);
            UpdateDiskAppearance(diskUid, disk);
            comp.Decoding = false;
            Dirty(uid, comp);
            _popup.PopupEntity(Loc.GetString("discovery-research-decode-complete", ("tech", TechnologyDisplayName(tech))), uid);
            _audio.PlayPvs(ScanFinishSound, uid);
            RefreshAnalysisUi(uid, comp);
        }
    }

    private void PauseAnalysisDecode(EntityUid uid, DiscoveryAnalysisConsoleComponent comp)
    {
        if (!comp.Decoding)
            return;

        comp.Decoding = false;
        Dirty(uid, comp);
    }

    private void OnUpload(EntityUid uid, DiscoveryAnalysisConsoleComponent comp, DiscoveryUploadMessage args)
    {
        if (comp.Decoding)
            return;

        if (!TryGetClientServer(uid, out var server, out _, out var db))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-server"), uid, args.Actor);
            return;
        }

        if (!TryGetDisk(uid, comp.DiskSlotId, out var diskUid, out var disk) ||
            disk.Stage != ResearchDataDiskStage.Decoded ||
            disk.LockedTechId == null ||
            !PrototypeManager.TryIndex(disk.LockedTechId.Value, out TechnologyPrototype? tech))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-decoded-disk"), uid, args.Actor);
            return;
        }

        if (db.UnlockedTechnologies.Contains(tech.ID))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-duplicate"), uid, args.Actor);
            RefreshAnalysisUi(uid, comp);
            return;
        }

        _research.AddTechnology(server, tech, db);
        ClearDisk(diskUid, disk);
        _popup.PopupEntity(Loc.GetString("discovery-research-upload-success", ("tech", TechnologyDisplayName(tech))), uid, args.Actor);
        RefreshAnalysisUi(uid, comp);
    }

    private void OnClearDisk(EntityUid uid, DiscoveryAnalysisConsoleComponent comp, DiscoveryClearDiskMessage args)
    {
        if (comp.Decoding)
            return;

        if (!TryGetDisk(uid, comp.DiskSlotId, out var diskUid, out var disk) || disk.Stage == ResearchDataDiskStage.Empty)
            return;

        ClearDisk(diskUid, disk);
        _popup.PopupEntity(Loc.GetString("discovery-research-disk-cleared"), uid, args.Actor);
        RefreshAnalysisUi(uid, comp);
    }

    private void OnConvertDuplicate(EntityUid uid, DiscoveryAnalysisConsoleComponent comp, DiscoveryConvertDuplicateMessage args)
    {
        if (comp.Decoding)
            return;

        if (!TryGetClientServer(uid, out var server, out _, out _))
            return;

        if (!TryGetDisk(uid, comp.DiskSlotId, out var diskUid, out var disk) ||
            disk.Stage != ResearchDataDiskStage.Decoded ||
            disk.LockedTechId == null ||
            !PrototypeManager.TryIndex(disk.LockedTechId.Value, out TechnologyPrototype? tech))
            return;

        var points = (int)(tech.Cost * DiscoveryResearchConstants.DuplicatePointsFraction);
        _research.ModifyServerPoints(server, points);
        ClearDisk(diskUid, disk);
        _popup.PopupEntity(Loc.GetString("discovery-research-converted-points", ("points", points)), uid, args.Actor);
        RefreshAnalysisUi(uid, comp);
    }

    private void OnAnalysisEject(EntityUid uid, DiscoveryAnalysisConsoleComponent comp, DiscoveryEjectDiskMessage args)
    {
        PauseAnalysisDecode(uid, comp);
        TryEjectDisk(uid, comp.DiskSlotId, args.Actor);
        RefreshAnalysisUi(uid, comp);
    }
}
