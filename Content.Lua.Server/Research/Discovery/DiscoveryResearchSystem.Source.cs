using System.Linq;
using Content.Server.Anomaly.Components;
using Content.Lua.Shared.Research.Discovery;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.UserInterface;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Research.Discovery;

namespace Content.Lua.Server.Research.Discovery;

public sealed partial class DiscoveryResearchSystem
{
    private void OnSourceUiOpen(EntityUid uid, DiscoverySourceConsoleComponent comp, AfterActivatableUIOpenEvent args)
    {
        EnsureComp<DiscoveryConsoleOperatorComponent>(args.User);
        RefreshSourceUi(uid, comp);
    }

    private void OnSourceResearchRegistrationChanged(EntityUid uid, DiscoverySourceConsoleComponent comp, ref ResearchRegistrationChangedEvent args)
    {
        if (args.Server == null)
            PauseSourceScan(uid, comp);
        RefreshSourceUi(uid, comp);
    }

    private void OnSourceUiClosed(EntityUid uid, DiscoverySourceConsoleComponent comp, BoundUIClosedEvent args)
    {
        if (!args.UiKey.Equals(DiscoverySourceUiKey.Key))
            return;

        RemComp<DiscoveryConsoleOperatorComponent>(args.Actor);
    }

    private void RefreshSourceUi(EntityUid uid, DiscoverySourceConsoleComponent comp)
    {
        var state = BuildSourceState(uid, comp);
        _ui.SetUiState(uid, DiscoverySourceUiKey.Key, state);
    }

    private DiscoverySourceBoundUserInterfaceState BuildSourceState(EntityUid uid, DiscoverySourceConsoleComponent comp)
    {
        var state = new DiscoverySourceBoundUserInterfaceState
        {
            Scanning = comp.Scanning,
            AutoScanning = comp.AutoScanning,
            ScanProgress = comp.ScanProgress,
            ScanDuration = comp.ScanDuration,
            AimNormalized = comp.AimNormalized,
            SignalNormalized = comp.SignalNormalized,
            SignalLocked = IsAimOnTarget(comp),
            SelectedNodeId = comp.SelectedNodeId,
            LinkedComputeCount = CountLinkedComputeOnGrid(uid),
            WorkingComputeCount = CountPoweredWorkingCompute(uid),
            ArtifactPadMode = HasComp<Content.Shared.Xenoarchaeology.Equipment.Components.AnalysisConsoleComponent>(uid),
        };

        if (TryGetDisk(uid, comp.DiskSlotId, out _, out var disk))
        {
            state.HasDisk = true;
            state.DiskStage = disk.Stage;
            if (disk.Stage == ResearchDataDiskStage.Raw)
            {
                state.PreviewDiscipline = disk.LeadDiscipline;
                state.PreviewTierHint = disk.LeadTierHint;
                state.PreviewTags = disk.LeadTags.ToList();
                state.PreviewCostEstimate = disk.LeadCostEstimate;
                state.PreviewRisk = disk.LeadRisk;
            }
        }
        if (TryResolveSource(uid, out var resolved, out var hasAnalyzer))
        {
            state.HasAnalyzer = hasAnalyzer;
            comp.SourceEntity = resolved;
            Dirty(uid, comp);
            state.HasSource = true;
            state.SourceName = MetaData(resolved).EntityName;
            state.SourceEntity = GetNetEntity(resolved);
            if (TryComp<ResearchSourceComponent>(resolved, out var sourceComp))
            {
                EnsureSourceYield((resolved, sourceComp));
                if (sourceComp.TechYieldTotal > 0)
                    state.UniqueDataPercent = sourceComp.TechYieldRemaining * 100 / sourceComp.TechYieldTotal;
            }
            FillArtifactNodes(resolved, state);
        }
        else
        {
            state.HasAnalyzer = hasAnalyzer;
            comp.SourceEntity = null;
            Dirty(uid, comp);
            state.HasSource = false;
            state.SourceEntity = null;
        }

        if (TryGetClientServer(uid, out _, out var server, out var db))
        {
            state.HasServer = true;
            state.ServerPoints = server.Points;
            state.UnlockedCount = db.UnlockedTechnologies.Count;
        }

        return state;
    }
    private bool TryResolveSource(EntityUid console, out EntityUid source, out bool hasAnalyzer)
    {
        source = default;
        hasAnalyzer = false;

        if (TryComp<Content.Shared.Xenoarchaeology.Equipment.Components.AnalysisConsoleComponent>(console, out var analysis))
        {
            if (_artifactAnalyzer.TryGetAnalyzer((console, analysis), out var analyzer))
            {
                hasAnalyzer = true;
                if (analyzer.Value.Comp.CurrentArtifact is { } placed && Exists(placed))
                {
                    EnsureComp<ResearchSourceComponent>(placed);
                    source = placed;
                    return true;
                }

                return false;
            }

            return false;
        }
        if (TryComp(console, out DiscoverySourceConsoleComponent? discovery) &&
            discovery.VesselEntity is { } vesselNet &&
            TryGetEntity(vesselNet, out var vesselUid) &&
            Exists(vesselUid))
        {
            hasAnalyzer = true;
            if (TryComp<AnomalyVesselComponent>(vesselUid, out var vessel) &&
                vessel.Anomaly is { } anomaly &&
                Exists(anomaly))
            {
                EnsureComp<ResearchSourceComponent>(anomaly);
                source = anomaly;
                return true;
            }

            return false;
        }

        return false;
    }
    private void OnSourceLinkAttempt(EntityUid uid, DiscoverySourceConsoleComponent comp, ref LinkAttemptEvent args)
    {
        if (args.Source != uid || args.SourcePort != comp.VesselLinkingPort)
            return;

        if (!HasComp<AnomalyVesselComponent>(args.Sink))
            return;

        var query = EntityQueryEnumerator<DiscoverySourceConsoleComponent>();
        while (query.MoveNext(out var other, out var otherComp))
        {
            if (other == uid || otherComp.VesselEntity is not { } linked)
                continue;

            if (!TryGetEntity(linked, out var vessel) || vessel != args.Sink)
                continue;

            args.Cancel();
            return;
        }
    }

    private void OnSourceVesselNewLink(EntityUid uid, DiscoverySourceConsoleComponent comp, NewLinkEvent args)
    {
        if (args.Source != uid || args.SourcePort != comp.VesselLinkingPort)
            return;

        if (!HasComp<AnomalyVesselComponent>(args.Sink))
            return;

        comp.VesselEntity = GetNetEntity(args.Sink);
        Dirty(uid, comp);
        RefreshSourceUi(uid, comp);
    }

    private void OnSourceVesselPortDisconnected(EntityUid uid, DiscoverySourceConsoleComponent comp, PortDisconnectedEvent args)
    {
        if (args.Port != comp.VesselLinkingPort)
            return;

        comp.VesselEntity = null;
        Dirty(uid, comp);
        RefreshSourceUi(uid, comp);
    }

    private void OnSourceConsoleMapInit(EntityUid uid, DiscoverySourceConsoleComponent comp, MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(uid, out var sourceLink))
            return;

        foreach (var (sinkUid, links) in sourceLink.LinkedPorts)
        {
            if (!links.Any(link => link.Source == comp.VesselLinkingPort))
                continue;

            if (!HasComp<AnomalyVesselComponent>(sinkUid))
                continue;

            comp.VesselEntity = GetNetEntity(sinkUid);
            Dirty(uid, comp);
            return;
        }
    }

    private void OnTuneSignal(EntityUid uid, DiscoverySourceConsoleComponent comp, DiscoveryTuneSignalMessage args)
    {
        if (comp.Scanning)
        {
            comp.AimInput = System.Numerics.Vector2.Zero;
            return;
        }

        if (!TryResolveSource(uid, out var sourceUid, out _))
        {
            comp.AimInput = System.Numerics.Vector2.Zero;
            _popup.PopupEntity(Loc.GetString("discovery-research-need-source"), uid, args.Actor);
            RefreshSourceUi(uid, comp);
            return;
        }

        comp.SourceEntity = sourceUid;

        var dir = args.Direction;
        if (dir.LengthSquared() > 1.01f)
            dir = System.Numerics.Vector2.Normalize(dir);
        dir = new System.Numerics.Vector2(
            Math.Clamp(dir.X, -1f, 1f),
            Math.Clamp(dir.Y, -1f, 1f));

        comp.AimInput = dir;
        Dirty(uid, comp);
    }

    private void UpdateAimMovement(float frameTime)
    {
        var query = EntityQueryEnumerator<DiscoverySourceConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Scanning || comp.AimInput.LengthSquared() < 0.0001f)
                continue;

            if (!TryResolveSource(uid, out var sourceUid, out _))
            {
                comp.AimInput = System.Numerics.Vector2.Zero;
                continue;
            }

            comp.SourceEntity = sourceUid;

            var dir = comp.AimInput;
            if (dir.LengthSquared() > 1.01f)
                dir = System.Numerics.Vector2.Normalize(dir);

            var speed = DiscoveryResearchConstants.SignalTuneSpeed;
            var step = speed * frameTime;
            var proposed = new System.Numerics.Vector2(
                Math.Clamp(comp.AimNormalized.X + dir.X * step, 0.08f, 0.92f),
                Math.Clamp(comp.AimNormalized.Y + dir.Y * step, 0.08f, 0.92f));
            var oldDist = (comp.AimNormalized - comp.SignalNormalized).LengthSquared();
            var newDist = (proposed - comp.SignalNormalized).LengthSquared();
            if (newDist > oldDist)
            {
                var resist = DiscoveryResearchConstants.SignalMissResistance;
                proposed = new System.Numerics.Vector2(
                    Math.Clamp(comp.AimNormalized.X + dir.X * step * resist, 0.08f, 0.92f),
                    Math.Clamp(comp.AimNormalized.Y + dir.Y * step * resist, 0.08f, 0.92f));
            }

            comp.AimNormalized = proposed;
            comp.SignalLocked = IsAimOnTarget(comp);
            Dirty(uid, comp);

            if (_ui.IsUiOpen(uid, DiscoverySourceUiKey.Key))
                RefreshSourceUi(uid, comp);
        }
    }

    private static bool IsAimOnTarget(DiscoverySourceConsoleComponent comp)
    {
        var delta = comp.AimNormalized - comp.SignalNormalized;
        var r = DiscoveryResearchConstants.SignalLockRadius;
        return delta.LengthSquared() <= r * r;
    }

    private void FillArtifactNodes(EntityUid sourceUid, DiscoverySourceBoundUserInterfaceState state)
    {
        if (!TryComp<XenoArtifactComponent>(sourceUid, out var artifact))
            return;

        var art = new Entity<XenoArtifactComponent>(sourceUid, artifact);
        foreach (var node in _xenoArtifact.GetAllNodes(art))
        {
            var effect = Loc.GetString("discovery-research-node-effect-unknown");
            if (!node.Comp.Locked)
            {
                effect = string.Empty;
                var proto = Prototype(node);
                if (proto != null && Loc.TryGetString($"ent-{proto.ID}.desc", out var locDesc) &&
                    !string.IsNullOrWhiteSpace(locDesc) &&
                    !string.Equals(locDesc, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    effect = locDesc;
                }
                else if (TryComp(node, out MetaDataComponent? meta) &&
                         !string.IsNullOrWhiteSpace(meta.EntityDescription) &&
                         !string.Equals(meta.EntityDescription, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    effect = meta.EntityDescription;
                }

                if (string.IsNullOrWhiteSpace(effect))
                    effect = Loc.GetString("discovery-research-node-effect-unknown");
            }

            var id = _xenoArtifact.GetNodeId(node);
            state.ArtifactNodes.Add(new DiscoveryArtifactNodeEntry
            {
                Id = id,
                Depth = node.Comp.Depth,
                Locked = node.Comp.Locked,
                Active = !node.Comp.Locked && _xenoArtifact.IsNodeActive(art, node),
                CanUnlock = node.Comp.Locked && _xenoArtifact.CanUnlockNode((node, node.Comp)),
                Selected = string.Equals(state.SelectedNodeId, id, StringComparison.Ordinal),
                Durability = node.Comp.Durability,
                MaxDurability = node.Comp.MaxDurability,
                ResearchValue = node.Comp.Locked
                    ? Math.Max(0, node.Comp.ResearchValue - node.Comp.ConsumedResearchValue)
                    : _xenoArtifact.GetResearchValue(node),
                Effect = effect,
            });
        }
    }

    private void OnSelectNode(EntityUid uid, DiscoverySourceConsoleComponent comp, DiscoverySelectNodeMessage args)
    {
        if (comp.Scanning)
            return;

        if (string.IsNullOrWhiteSpace(args.NodeId))
        {
            comp.SelectedNodeId = null;
            Dirty(uid, comp);
            RefreshSourceUi(uid, comp);
            return;
        }

        if (!TryResolveSource(uid, out var sourceUid, out _) ||
            !TryComp<XenoArtifactComponent>(sourceUid, out var artifact))
        {
            comp.SelectedNodeId = null;
            Dirty(uid, comp);
            RefreshSourceUi(uid, comp);
            return;
        }

        var art = new Entity<XenoArtifactComponent>(sourceUid, artifact);
        var found = false;
        foreach (var node in _xenoArtifact.GetAllNodes(art))
        {
            if (!string.Equals(_xenoArtifact.GetNodeId(node), args.NodeId, StringComparison.Ordinal))
                continue;
            found = true;
            break;
        }

        comp.SelectedNodeId = found ? args.NodeId : null;
        Dirty(uid, comp);
        RefreshSourceUi(uid, comp);
    }

    private void OnStartScan(EntityUid uid, DiscoverySourceConsoleComponent comp, DiscoveryStartScanMessage args)
    {
        if (comp.Scanning)
            return;

        if (!IsAimOnTarget(comp))
        {
            var away = comp.AimNormalized - comp.SignalNormalized;
            if (away.LengthSquared() < 0.0001f)
                away = new System.Numerics.Vector2(Random.NextFloat(-1f, 1f), Random.NextFloat(-1f, 1f));
            away = System.Numerics.Vector2.Normalize(away);
            comp.AimNormalized = new System.Numerics.Vector2(
                Math.Clamp(comp.AimNormalized.X + away.X * 0.08f, 0.08f, 0.92f),
                Math.Clamp(comp.AimNormalized.Y + away.Y * 0.08f, 0.08f, 0.92f));
            comp.SignalLocked = false;
            Dirty(uid, comp);
            _popup.PopupEntity(Loc.GetString("discovery-research-signal-miss"), uid, args.Actor);
            RefreshSourceUi(uid, comp);
            return;
        }

        if (!TryGetDisk(uid, comp.DiskSlotId, out _, out var disk) || disk.Stage != ResearchDataDiskStage.Empty)
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-empty-disk"), uid, args.Actor);
            return;
        }

        if (!TryGetClientServer(uid, out _, out _, out var db))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-server"), uid, args.Actor);
            return;
        }

        if (!TryResolveSource(uid, out var sourceUid, out _))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-source"), uid, args.Actor);
            return;
        }

        if (TryComp<XenoArtifactComponent>(sourceUid, out _) &&
            string.IsNullOrWhiteSpace(comp.SelectedNodeId))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-node"), uid, args.Actor);
            return;
        }
        comp.AimInput = System.Numerics.Vector2.Zero;
        EnsureComp<ResearchSourceComponent>(sourceUid);
        var source = (sourceUid, Comp<ResearchSourceComponent>(sourceUid));
        EnsureSourceYield(source);

        var unlocked = db.UnlockedTechnologies.Count;
        var compute = GetEffectiveComputeLoad(uid);
        var resume = comp.ScanProgress > 0.01f &&
                     comp.SourceEntity == sourceUid &&
                     comp.ScanDuration > 0.01f;
        comp.SourceEntity = sourceUid;
        comp.Scanning = true;
        if (!resume)
        {
            comp.ScanProgress = 0;
            comp.ScanDuration = ScanDurationSeconds(unlocked, compute);
        }
        else
        {
            var newDuration = ScanDurationSeconds(unlocked, compute);
            if (comp.ScanDuration > 0.01f && MathF.Abs(newDuration - comp.ScanDuration) > 0.05f)
            {
                var fraction = Math.Clamp(comp.ScanProgress / comp.ScanDuration, 0f, 1f);
                comp.ScanDuration = newDuration;
                comp.ScanProgress = fraction * newDuration;
            }
        }

        Dirty(uid, comp);
        RefreshSourceUi(uid, comp);
    }

    private void OnSetAuto(EntityUid uid, DiscoverySourceConsoleComponent comp, DiscoverySetAutoMessage args)
    {
        if (!args.Enabled)
        {
            PauseSourceScan(uid, comp);
            RefreshSourceUi(uid, comp);
            return;
        }

        if (comp.Scanning)
            return;

        if (!TryGetDisk(uid, comp.DiskSlotId, out _, out var disk) || disk.Stage != ResearchDataDiskStage.Empty)
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-empty-disk"), uid, args.Actor);
            return;
        }

        if (!TryGetClientServer(uid, out _, out _, out var db))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-server"), uid, args.Actor);
            return;
        }

        if (!TryResolveSource(uid, out var sourceUid, out _))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-source"), uid, args.Actor);
            return;
        }
        if (TryComp<XenoArtifactComponent>(sourceUid, out var artifact))
        {
            if (!TryGetSelectedArtifactNode((sourceUid, artifact), comp.SelectedNodeId, out var node))
            {
                _popup.PopupEntity(Loc.GetString("discovery-research-need-node"), uid, args.Actor);
                return;
            }

            if (!node.Comp.Locked)
            {
                _popup.PopupEntity(Loc.GetString("discovery-research-node-already"), uid, args.Actor);
                return;
            }

            if (!_xenoArtifact.CanUnlockNode((node, node.Comp)))
            {
                _popup.PopupEntity(Loc.GetString("discovery-research-node-blocked"), uid, args.Actor);
                return;
            }
        }

        EnsureComp<ResearchSourceComponent>(sourceUid);
        EnsureSourceYield((sourceUid, Comp<ResearchSourceComponent>(sourceUid)));
        var resume = comp.ScanProgress > 0.01f &&
                     comp.SourceEntity == sourceUid &&
                     comp.ScanDuration > 0.01f;
        comp.SourceEntity = sourceUid;
        comp.AimInput = System.Numerics.Vector2.Zero;
        comp.AutoScanning = true;
        comp.Scanning = true;
        if (!resume)
        {
            comp.ScanProgress = 0;
            comp.ScanDuration = AutoScanDurationSeconds(db.UnlockedTechnologies.Count);
        }
        else
        {
            var newDuration = AutoScanDurationSeconds(db.UnlockedTechnologies.Count);
            if (comp.ScanDuration > 0.01f && MathF.Abs(newDuration - comp.ScanDuration) > 0.05f)
            {
                var fraction = Math.Clamp(comp.ScanProgress / comp.ScanDuration, 0f, 1f);
                comp.ScanDuration = newDuration;
                comp.ScanProgress = fraction * newDuration;
            }
        }

        Dirty(uid, comp);
        RefreshSourceUi(uid, comp);
    }

    private void OnSourceEject(EntityUid uid, DiscoverySourceConsoleComponent comp, DiscoveryEjectDiskMessage args)
    {
        PauseSourceScan(uid, comp);
        TryEjectDisk(uid, comp.DiskSlotId, args.Actor);
        RefreshSourceUi(uid, comp);
    }

    private void UpdateScans(float frameTime)
    {
        var query = EntityQueryEnumerator<DiscoverySourceConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Scanning)
                continue;

            if (!TryGetDisk(uid, comp.DiskSlotId, out _, out var disk) ||
                disk.Stage != ResearchDataDiskStage.Empty ||
                !TryGetClientServer(uid, out _, out _, out var db) ||
                !TryResolveSource(uid, out var sourceUid, out _))
            {
                PauseSourceScan(uid, comp);
                if (_ui.IsUiOpen(uid, DiscoverySourceUiKey.Key))
                    RefreshSourceUi(uid, comp);
                continue;
            }

            comp.SourceEntity = sourceUid;
            var unlocked = db.UnlockedTechnologies.Count;
            var compute = GetEffectiveComputeLoad(uid);
            var newDuration = comp.AutoScanning
                ? AutoScanDurationSeconds(unlocked)
                : ScanDurationSeconds(unlocked, compute);
            if (comp.ScanDuration > 0.01f && MathF.Abs(newDuration - comp.ScanDuration) > 0.05f)
            {
                var fraction = Math.Clamp(comp.ScanProgress / comp.ScanDuration, 0f, 1f);
                comp.ScanDuration = newDuration;
                comp.ScanProgress = fraction * newDuration;
            }

            comp.ScanProgress += frameTime;
            Dirty(uid, comp);
            if (frameTime > 0)
            {
                var pts = (int)(DiscoveryResearchConstants.ScanPointsPerSecond * frameTime);
                if (pts > 0 && TryGetClientServer(uid, out var server, out _, out _))
                    _research.ModifyServerPoints(server, pts);
            }

            if (comp.ScanProgress < comp.ScanDuration)
            {
                if (_ui.IsUiOpen(uid, DiscoverySourceUiKey.Key))
                    RefreshSourceUi(uid, comp);
                continue;
            }

            CompleteScan(uid, comp);
        }
    }
    private void PauseSourceScan(EntityUid uid, DiscoverySourceConsoleComponent comp)
    {
        if (!comp.Scanning && !comp.AutoScanning)
            return;

        comp.Scanning = false;
        comp.AutoScanning = false;
        Dirty(uid, comp);
    }

    private void CompleteScan(EntityUid uid, DiscoverySourceConsoleComponent comp)
    {
        comp.Scanning = false;
        comp.ScanProgress = 0;
        comp.SignalNormalized = new System.Numerics.Vector2(Random.NextFloat(0.15f, 0.85f), Random.NextFloat(0.15f, 0.85f));
        comp.SignalLocked = IsAimOnTarget(comp);

        if (comp.SourceEntity is not { } sourceUid || !TryComp(sourceUid, out ResearchSourceComponent? sourceComp))
        {
            FinishScanCycle(uid, comp, wroteTech: false);
            return;
        }

        var source = (sourceUid, sourceComp);
        EnsureSourceYield(source);
        if (TryGetClientServer(uid, out var server, out var serverComp, out var db))
            _research.ModifyServerPoints(server, DiscoveryResearchConstants.ScanPointsPerSecond * 5);

        var wroteTech = false;
        if (sourceComp.TechYieldRemaining > 0 &&
            Random.Prob(sourceComp.TechDiscoveryChance) &&
            TryGetDisk(uid, comp.DiskSlotId, out var diskUid, out var disk) &&
            disk.Stage == ResearchDataDiskStage.Empty &&
            TryGetClientServer(uid, out _, out var sComp, out var database))
        {
            var exclude = new HashSet<ProtoId<TechnologyPrototype>>();
            foreach (var unlocked in database.UnlockedTechnologies)
                exclude.Add(unlocked);
            foreach (var discovered in sourceComp.DiscoveredTechs)
                exclude.Add(discovered);

            if (TryRollTechnology(sComp.Faction, exclude, out var techId) &&
                PrototypeManager.TryIndex(techId, out TechnologyPrototype? tech))
            {
                WriteRawPreview(diskUid, disk, tech);
                sourceComp.TechYieldRemaining--;
                sourceComp.DiscoveredTechs.Add(techId);
                Dirty(sourceUid, sourceComp);
                wroteTech = true;
                _popup.PopupEntity(Loc.GetString("discovery-research-tech-found"), uid);
            }
        }

        TriggerSourceEffects(uid, sourceUid);
        FinishScanCycle(uid, comp, wroteTech);
    }

    private void FinishScanCycle(EntityUid uid, DiscoverySourceConsoleComponent comp, bool wroteTech)
    {
        var continueAuto = comp.AutoScanning && !wroteTech && CanContinueAuto(uid, comp);
        if (continueAuto)
        {
            var unlocked = 0;
            if (TryGetClientServer(uid, out _, out _, out var db))
                unlocked = db.UnlockedTechnologies.Count;

            comp.Scanning = true;
            comp.ScanProgress = 0;
            comp.ScanDuration = AutoScanDurationSeconds(unlocked);
        }
        else
        {
            comp.AutoScanning = false;
            _audio.PlayPvs(ScanFinishSound, uid);
            if (!wroteTech && !IsSelectedNodeUnlocked(comp))
                _popup.PopupEntity(Loc.GetString("discovery-research-scan-complete"), uid);
        }

        Dirty(uid, comp);
        RefreshSourceUi(uid, comp);
    }

    private bool CanContinueAuto(EntityUid uid, DiscoverySourceConsoleComponent comp)
    {
        if (!TryGetDisk(uid, comp.DiskSlotId, out _, out var disk) || disk.Stage != ResearchDataDiskStage.Empty)
            return false;

        if (!TryGetClientServer(uid, out _, out _, out _))
            return false;

        if (!TryResolveSource(uid, out var sourceUid, out _))
            return false;

        comp.SourceEntity = sourceUid;
        if (TryComp<XenoArtifactComponent>(sourceUid, out _))
            return TryGetContinuableSelectedNode(comp, out _);
        if (!TryComp(sourceUid, out ResearchSourceComponent? sourceComp))
            return false;

        EnsureSourceYield((sourceUid, sourceComp));
        return sourceComp.TechYieldRemaining > 0;
    }

    private bool IsSelectedNodeUnlocked(DiscoverySourceConsoleComponent comp)
    {
        if (comp.SourceEntity is not { } sourceUid ||
            !TryComp<XenoArtifactComponent>(sourceUid, out var artifact) ||
            !TryGetSelectedArtifactNode((sourceUid, artifact), comp.SelectedNodeId, out var node))
            return false;

        return !node.Comp.Locked;
    }

    private bool TryGetContinuableSelectedNode(
        DiscoverySourceConsoleComponent comp,
        out Entity<XenoArtifactNodeComponent> node)
    {
        node = default;
        if (comp.SourceEntity is not { } sourceUid || !TryComp<XenoArtifactComponent>(sourceUid, out var artifact))
            return false;

        if (!TryGetSelectedArtifactNode((sourceUid, artifact), comp.SelectedNodeId, out node))
            return false;

        return node.Comp.Locked && _xenoArtifact.CanUnlockNode((node, node.Comp));
    }

    private bool TryGetSelectedArtifactNode(
        Entity<XenoArtifactComponent> artifact,
        string? nodeId,
        out Entity<XenoArtifactNodeComponent> node)
    {
        node = default;
        if (string.IsNullOrWhiteSpace(nodeId))
            return false;

        foreach (var candidate in _xenoArtifact.GetAllNodes(artifact))
        {
            if (!string.Equals(_xenoArtifact.GetNodeId(candidate), nodeId, StringComparison.Ordinal))
                continue;

            node = candidate;
            return true;
        }

        return false;
    }

    private void TriggerSourceEffects(EntityUid console, EntityUid sourceUid)
    {
        if (!TryComp<XenoArtifactComponent>(sourceUid, out var artifact))
            return;

        if (!TryComp(console, out DiscoverySourceConsoleComponent? sourceConsole) ||
            string.IsNullOrWhiteSpace(sourceConsole.SelectedNodeId))
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-node"), console);
            return;
        }

        var art = new Entity<XenoArtifactComponent>(sourceUid, artifact);
        EntityUid? unlockedNode = null;
        foreach (var node in _xenoArtifact.GetAllNodes(art))
        {
            if (!string.Equals(_xenoArtifact.GetNodeId(node), sourceConsole.SelectedNodeId, StringComparison.Ordinal))
                continue;

            if (!node.Comp.Locked)
            {
                _popup.PopupEntity(Loc.GetString("discovery-research-node-already"), console);
                return;
            }

            if (!_xenoArtifact.CanUnlockNode((node, node.Comp)))
            {
                _popup.PopupEntity(Loc.GetString("discovery-research-node-blocked"), console);
                return;
            }

            if (!Random.Prob(DiscoveryResearchConstants.ArtifactNodeUnlockChance))
            {
                _popup.PopupEntity(Loc.GetString("discovery-research-node-fail"), console);
                return;
            }

            _xenoArtifact.SetNodeUnlocked(art, node);
            unlockedNode = node;

            var research = _xenoArtifact.GetResearchValue(node);
            if (research > 0 && TryGetClientServer(console, out var server, out _, out _))
            {
                _xenoArtifact.SetConsumedResearchValue(node, node.Comp.ConsumedResearchValue + research);
                _research.ModifyServerPoints(server, research);
                _popup.PopupEntity(Loc.GetString("discovery-research-node-points", ("points", research)), console);
            }

            break;
        }

        if (unlockedNode == null)
        {
            _popup.PopupEntity(Loc.GetString("discovery-research-need-node"), console);
            return;
        }

        _xenoArtifact.TryActivateXenoArtifact(
            art,
            user: null,
            target: null,
            Transform(sourceUid).Coordinates,
            consumeDurability: false);
    }

    private void OnAnalyzerSampleChanged(EntityUid uid, ArtifactAnalyzerComponent comp, ref ArtifactAnalyzerSampleChangedEvent args)
    {
        TryRefreshDiscoveryConsoleForAnalyzer(uid, comp);
    }

    private void OnAnalysisConsoleLinkChanged(EntityUid uid, AnalysisConsoleComponent comp, ref AnalysisConsoleAnalyzerLinkChangedEvent args)
    {
        if (TryComp(uid, out DiscoverySourceConsoleComponent? source))
            RefreshSourceUi(uid, source);
    }

    private void TryRefreshDiscoveryConsoleForAnalyzer(EntityUid analyzerUid, ArtifactAnalyzerComponent analyzer)
    {
        if (analyzer.Console is not { } console || !Exists(console))
            return;

        if (!_ui.IsUiOpen(console, DiscoverySourceUiKey.Key))
            return;

        if (!TryComp(console, out DiscoverySourceConsoleComponent? source))
            return;

        RefreshSourceUi(console, source);
    }
}
