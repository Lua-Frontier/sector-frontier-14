using System.Linq;
using Content.Server._NF.Lathe;
using Content.Server.Anomaly.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Server.Xenoarchaeology.Artifact;
using Content.Lua.Shared.Research.Discovery;
using Content.Shared.Atmos;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Equipment;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Research.Discovery;

namespace Content.Lua.Server.Research.Discovery;

public sealed partial class DiscoveryResearchSystem : SharedDiscoveryResearchSystem
{
    private static readonly EntProtoId SparksEffectProto = "EffectSparks";
    private static readonly SoundPathSpecifier ComputeDownSound = new("/Audio/_Lua/Effects/Server/serverDown.ogg");
    private static readonly SoundPathSpecifier ComputeSparkSound = new("/Audio/_Lua/Effects/Server/server_2.ogg");
    private static readonly AudioParams ComputeDownAudioParams = AudioParams.Default.WithMaxDistance(10f);
    private static readonly AudioParams ComputeSparkAudioParams = AudioParams.Default.WithMaxDistance(5f);

    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _material = default!;
    [Dependency] private readonly BlueprintLatheSystem _blueprintLathe = default!;
    [Dependency] private readonly SharedArtifactAnalyzerSystem _artifactAnalyzer = default!;
    [Dependency] private readonly SharedLatheSystem _lathe = default!;
    [Dependency] private readonly XenoArtifactSystem _xenoArtifact = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly PowerReceiverSystem _apcPower = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private static readonly SoundPathSpecifier ScanFinishSound = new("/Audio/Machines/scan_finish.ogg");

    private readonly HashSet<EntityUid> _activeComputeGrids = new();
    private readonly List<GasMixture> _computeHeatEnvironments = new();
    private float _computeUiRefreshAccum;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ResearchServerComponent, MapInitEvent>(OnServerMapInit);

        SubscribeLocalEvent<DiscoverySourceConsoleComponent, AfterActivatableUIOpenEvent>(OnSourceUiOpen);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, BoundUIClosedEvent>(OnSourceUiClosed);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, DiscoveryTuneSignalMessage>(OnTuneSignal);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, DiscoverySelectNodeMessage>(OnSelectNode);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, DiscoveryStartScanMessage>(OnStartScan);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, DiscoverySetAutoMessage>(OnSetAuto);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, DiscoveryEjectDiskMessage>(OnSourceEject);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, NewLinkEvent>(OnSourceVesselNewLink);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, PortDisconnectedEvent>(OnSourceVesselPortDisconnected);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, LinkAttemptEvent>(OnSourceLinkAttempt);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, MapInitEvent>(OnSourceConsoleMapInit);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, EntInsertedIntoContainerMessage>(OnSourceDiskInserted);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, EntRemovedFromContainerMessage>(OnSourceDiskRemoved);
        SubscribeLocalEvent<DiscoverySourceConsoleComponent, ResearchRegistrationChangedEvent>(OnSourceResearchRegistrationChanged);
        SubscribeLocalEvent<ArtifactAnalyzerComponent, ArtifactAnalyzerSampleChangedEvent>(OnAnalyzerSampleChanged);
        SubscribeLocalEvent<AnalysisConsoleComponent, AnalysisConsoleAnalyzerLinkChangedEvent>(OnAnalysisConsoleLinkChanged);

        SubscribeLocalEvent<DiscoveryAnalysisConsoleComponent, AfterActivatableUIOpenEvent>(OnAnalysisUiOpen);
        SubscribeLocalEvent<DiscoveryAnalysisConsoleComponent, DiscoveryStartDecodeMessage>(OnStartDecode);
        SubscribeLocalEvent<DiscoveryAnalysisConsoleComponent, DiscoveryUploadMessage>(OnUpload);
        SubscribeLocalEvent<DiscoveryAnalysisConsoleComponent, DiscoveryClearDiskMessage>(OnClearDisk);
        SubscribeLocalEvent<DiscoveryAnalysisConsoleComponent, DiscoveryConvertDuplicateMessage>(OnConvertDuplicate);
        SubscribeLocalEvent<DiscoveryAnalysisConsoleComponent, DiscoveryEjectDiskMessage>(OnAnalysisEject);
        SubscribeLocalEvent<DiscoveryAnalysisConsoleComponent, EntInsertedIntoContainerMessage>(OnAnalysisDiskInserted);
        SubscribeLocalEvent<DiscoveryAnalysisConsoleComponent, EntRemovedFromContainerMessage>(OnAnalysisDiskRemoved);
        SubscribeLocalEvent<DiscoveryAnalysisConsoleComponent, ResearchRegistrationChangedEvent>(OnAnalysisResearchRegistrationChanged);

        SubscribeLocalEvent<DiscoveryJournalConsoleComponent, AfterActivatableUIOpenEvent>(OnJournalUiOpen);
        SubscribeLocalEvent<DiscoveryJournalConsoleComponent, DiscoveryPrintBlueprintMessage>(OnPrintBlueprint);
        SubscribeLocalEvent<DiscoveryJournalConsoleComponent, GetMaterialWhitelistEvent>(OnJournalMaterialWhitelist);
        SubscribeLocalEvent<DiscoveryJournalConsoleComponent, MaterialAmountChangedEvent>(OnJournalMaterialChanged);
        SubscribeLocalEvent<DiscoveryJournalConsoleComponent, MapInitEvent>(OnJournalMapInit);
        SubscribeLocalEvent<DiscoveryJournalConsoleComponent, ResearchRegistrationChangedEvent>(OnJournalResearchRegistrationChanged);

        SubscribeLocalEvent<ServerTechReconfiguratorComponent, AfterInteractEvent>(OnTheftAfterInteract);
        SubscribeLocalEvent<ServerTechReconfiguratorComponent, BoundUIClosedEvent>(OnTheftUiClosed);
        SubscribeLocalEvent<ServerTechReconfiguratorComponent, ServerTechSelectTechnologyMessage>(OnTheftSelect);
        SubscribeLocalEvent<ServerTechReconfiguratorComponent, ServerTechStartTheftMessage>(OnTheftStart);
        SubscribeLocalEvent<ServerTechReconfiguratorComponent, ServerTechSubmitRouteMessage>(OnTheftSubmitRoute);
        SubscribeLocalEvent<ServerTechReconfiguratorComponent, ServerTechResetPuzzleMessage>(OnTheftResetPuzzle);
        SubscribeLocalEvent<ServerTechReconfiguratorComponent, ServerTechEjectDiskMessage>(OnTheftEject);
        SubscribeLocalEvent<ServerTechReconfiguratorComponent, EntInsertedIntoContainerMessage>(OnTheftDiskInserted);
        SubscribeLocalEvent<ServerTechReconfiguratorComponent, EntRemovedFromContainerMessage>(OnTheftDiskRemoved);

        SubscribeLocalEvent<ResearchComputeServerComponent, InteractUsingEvent>(OnComputeInteractUsing);
        SubscribeLocalEvent<ResearchComputeServerComponent, MapInitEvent>(OnComputeMapInit);
        SubscribeLocalEvent<ResearchComputeServerComponent, PowerChangedEvent>(OnComputePowerChanged);
        SubscribeLocalEvent<ResearchComputeServerComponent, AfterActivatableUIOpenEvent>(OnComputeUiOpen);
        SubscribeLocalEvent<ResearchComputeServerComponent, ResearchComputeTogglePowerMessage>(OnComputeTogglePower);
        SubscribeLocalEvent<ResearchComputeServerComponent, DamageChangedEvent>(OnComputeDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateAimMovement(frameTime);
        UpdateComputeThermalAndHeat(frameTime);
        UpdateScans(frameTime);
        UpdateDecodes(frameTime);
        UpdateBlueprintPrints(frameTime);
        UpdateTheftConnections();
        UpdateTheftDownloads(frameTime);
        UpdateComputeFailures(frameTime);
        UpdateComputeSparks(frameTime);
        UpdateComputeUiRefresh(frameTime);
    }

    private void OnServerMapInit(EntityUid uid, ResearchServerComponent server, MapInitEvent args)
    {
        if (!PrototypeManager.TryIndex(new ProtoId<FactionResearchProfilePrototype>((string)server.Faction), out var profile))
            return;

        if (!TryComp<TechnologyDatabaseComponent>(uid, out var db))
            return;

        foreach (var kit in profile.StartingKits)
        {
            if (db.UnlockedTechnologies.Contains(kit))
                continue;
            if (!PrototypeManager.TryIndex(kit, out TechnologyPrototype? tech))
                continue;
            if (!_research.IsTechnologyFactionAllowed(uid, tech))
                continue;
            _research.AddTechnology(uid, tech, db);
        }
    }

    private void OnSourceDiskInserted(EntityUid uid, DiscoverySourceConsoleComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.DiskSlotId)
            return;
        RefreshSourceUi(uid, comp);
    }

    private void OnSourceDiskRemoved(EntityUid uid, DiscoverySourceConsoleComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.DiskSlotId)
            return;
        PauseSourceScan(uid, comp);
        RefreshSourceUi(uid, comp);
    }

    private string TechnologyDisplayName(TechnologyPrototype tech)
    {
        var options = new List<string>();
        if (Loc.TryGetString(tech.Name, out var localized) && !string.IsNullOrWhiteSpace(localized))
            options.Add(localized);

        if (TryGetTechnologyEntityName(tech, out var entityName))
            options.Add(entityName);

        foreach (var recipeId in tech.RecipeUnlocks)
            options.Add(RecipeDisplayName(recipeId));

        return BestDisplayName(options, tech.ID);
    }

    private bool TryGetTechnologyEntityName(TechnologyPrototype tech, out string name)
    {
        if (tech.EntityIcon is { } icon)
        {
            if (PrototypeManager.HasIndex(icon) &&
                TryGetEntityDisplayName(icon.Id, out name))
                return true;

            if (PrototypeManager.TryIndex<LatheRecipePrototype>(icon.Id, out var recipe))
            {
                if (recipe.Result is { } result && TryGetEntityDisplayName(result, out name))
                    return true;

                var recipeName = _lathe.GetRecipeName(recipe);
                if (!string.IsNullOrWhiteSpace(recipeName))
                {
                    name = recipeName;
                    return true;
                }
            }
        }

        name = string.Empty;
        return false;
    }

    private string RecipeDisplayName(ProtoId<LatheRecipePrototype> recipeId)
    {
        if (!PrototypeManager.TryIndex(recipeId, out LatheRecipePrototype? recipe))
            return recipeId;

        var options = new List<string>();
        if (recipe.Name is { } nameLoc &&
            Loc.TryGetString(nameLoc, out var localized) &&
            !string.IsNullOrWhiteSpace(localized))
            options.Add(localized);

        var latheName = _lathe.GetRecipeName(recipe);
        if (!string.IsNullOrWhiteSpace(latheName))
            options.Add(latheName);

        if (recipe.Result is { } result &&
            TryGetEntityDisplayName(result, out var entityName))
            options.Add(entityName);

        return BestDisplayName(options, recipeId);
    }

    private bool TryGetEntityDisplayName(string prototypeId, out string name)
    {
        if (Loc.TryGetString($"ent-{prototypeId}", out var localized) &&
            !string.IsNullOrWhiteSpace(localized))
        {
            name = localized;
            return true;
        }

        if (PrototypeManager.TryIndex<EntityPrototype>(prototypeId, out var proto) &&
            !string.IsNullOrWhiteSpace(proto.Name))
        {
            name = proto.Name;
            return true;
        }

        name = string.Empty;
        return false;
    }

    private static string BestDisplayName(List<string> candidates, string fallback)
    {
        string? best = null;
        var bestScore = int.MinValue;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var score = ScoreDisplayName(candidate);
            if (score < bestScore)
                continue;
            if (score == bestScore &&
                best != null &&
                (candidate.Length > best.Length || LooksLikeRawIdentifier(candidate)))
                continue;

            best = candidate;
            bestScore = score;
        }

        return best ?? fallback;
    }

    private static int ScoreDisplayName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return int.MinValue;

        var score = 0;
        if (trimmed.Any(char.IsWhiteSpace))
            score += 6;
        if (trimmed.Contains('-'))
            score += 2;
        if (trimmed.Any(IsCyrillic))
            score += 3;
        if (trimmed.Length <= 8)
            score += 2;
        if (LooksLikeRawIdentifier(trimmed))
            score -= 8 + Math.Min(trimmed.Length / 3, 10);
        if (LooksGenericName(trimmed))
            score -= 7;

        return score;
    }

    private static bool LooksLikeRawIdentifier(string value)
    {
        if (value.Any(char.IsWhiteSpace))
            return false;

        var hasInnerUpper = value.Skip(1).Any(char.IsUpper);
        var hasDigits = value.Any(char.IsDigit);
        var hasUnderscore = value.Contains('_');
        return hasInnerUpper || hasDigits || hasUnderscore;
    }

    private static bool LooksGenericName(string value)
    {
        return value.Equals("ручка", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("pen", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("имплантер", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("implanter", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("мыло", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("soap", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCyrillic(char c)
        => c is >= '\u0400' and <= '\u04FF';

    private void OnAnalysisDiskInserted(EntityUid uid, DiscoveryAnalysisConsoleComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != comp.DiskSlotId)
            return;
        RefreshAnalysisUi(uid, comp);
    }

    private void OnAnalysisDiskRemoved(EntityUid uid, DiscoveryAnalysisConsoleComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.DiskSlotId)
            return;
        PauseAnalysisDecode(uid, comp);
        RefreshAnalysisUi(uid, comp);
    }

    private void OnJournalMaterialWhitelist(EntityUid uid, DiscoveryJournalConsoleComponent comp, ref GetMaterialWhitelistEvent args)
    {
        args.Whitelist.Add(DiscoveryResearchConstants.PaperMaterial);
    }

    private void OnJournalMaterialChanged(EntityUid uid, DiscoveryJournalConsoleComponent comp, ref MaterialAmountChangedEvent args)
    {
        if (_ui.IsUiOpen(uid, DiscoveryJournalUiKey.Key))
            RefreshJournalUi(uid);
    }

    private void OnJournalMapInit(EntityUid uid, DiscoveryJournalConsoleComponent comp, MapInitEvent args)
    {
        _material.UpdateMaterialWhitelist(uid);
    }

    private void OnComputeMapInit(EntityUid uid, ResearchComputeServerComponent comp, MapInitEvent args)
    {
        UpdateComputeAppearance(uid, comp);
    }

    private void OnComputeInteractUsing(EntityUid uid, ResearchComputeServerComponent comp, ref InteractUsingEvent args)
    {
        if (args.Handled || !comp.Broken || !_tool.HasQuality(args.Used, "Screwing"))
            return;
        args.Handled = true;
    }

    private void OnComputeUiOpen(EntityUid uid, ResearchComputeServerComponent comp, AfterActivatableUIOpenEvent args)
    {
        RefreshComputeUi(uid, comp);
    }

    private void OnComputeTogglePower(EntityUid uid, ResearchComputeServerComponent comp, ResearchComputeTogglePowerMessage args)
    {
        if (comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("research-compute-server-ui-power-broken"), uid, args.Actor);
            return;
        }

        _powerReceiver.TryTogglePower(uid, user: args.Actor);
        RefreshComputeUi(uid, comp);
    }

    private void OnComputePowerChanged(EntityUid uid, ResearchComputeServerComponent comp, ref PowerChangedEvent args)
    {
        if (comp.Broken || comp.ThermalOffline)
            _appearance.SetData(uid, PowerDeviceVisuals.Powered, false);
        else
            UpdateComputeAppearance(uid, comp);

        RefreshComputeUi(uid, comp);
    }

    private void UpdateComputeAppearance(EntityUid uid, ResearchComputeServerComponent comp)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        _appearance.SetData(uid, ResearchComputeServerVisuals.Broken, comp.Broken, appearance);

        if (comp.Broken || comp.ThermalOffline)
            _appearance.SetData(uid, PowerDeviceVisuals.Powered, false, appearance);
        else if (TryComp(uid, out ApcPowerReceiverComponent? power))
            _appearance.SetData(uid, PowerDeviceVisuals.Powered, power.Powered, appearance);
    }

    private void UpdateComputeThermalAndHeat(float frameTime)
    {
        RefreshActiveComputeGrids();

        var query = EntityQueryEnumerator<ResearchComputeServerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var compute, out var xform))
        {
            UpdateComputePowerDraw(uid, compute, xform, frameTime);

            compute.HeatUpdateAccumulator += TimeSpan.FromSeconds(frameTime);
            if (compute.HeatUpdateAccumulator < DiscoveryResearchConstants.ComputeHeatUpdateSpacing)
                continue;
            compute.HeatUpdateAccumulator -= DiscoveryResearchConstants.ComputeHeatUpdateSpacing;

            CollectComputeEnvironments(uid, xform);

            var thermalOffline = IsComputeEnvironmentUnsafe(_computeHeatEnvironments);
            SetComputeThermalOffline(uid, compute, thermalOffline);

            if (!compute.Broken && !compute.ThermalOffline &&
                compute.LoadFactor > 0.001f &&
                TryComp(uid, out ApcPowerReceiverComponent? power) &&
                power.Powered &&
                _computeHeatEnvironments.Count > 0)
            {
                var heatPerTile = compute.HeatEnergyPerSecond * compute.LoadFactor / _computeHeatEnvironments.Count;
                foreach (var env in _computeHeatEnvironments)
                    _atmosphere.AddHeat(env, heatPerTile);
            }

            RefreshComputeUi(uid, compute);
        }
    }

    private void UpdateComputePowerDraw(
        EntityUid uid,
        ResearchComputeServerComponent compute,
        TransformComponent xform,
        float frameTime)
    {
        if (!TryComp(uid, out ApcPowerReceiverComponent? power))
            return;

        var underLoad = !compute.Broken &&
                        !compute.ThermalOffline &&
                        power.Powered &&
                        xform.GridUid is { } grid &&
                        _activeComputeGrids.Contains(grid);

        var targetFactor = underLoad ? 1f : 0f;
        if (compute.Broken || compute.ThermalOffline || !power.Powered)
            targetFactor = 0f;

        var prevFactor = compute.LoadFactor;
        if (MathF.Abs(targetFactor - compute.LoadFactor) < 0.0001f)
        {
            compute.LoadFactor = targetFactor;
        }
        else
        {
            var rampSeconds = targetFactor > compute.LoadFactor
                ? MathF.Max(0.01f, compute.RampUpSeconds)
                : MathF.Max(0.01f, compute.RampDownSeconds);
            var step = frameTime / rampSeconds;
            if (compute.LoadFactor < targetFactor)
                compute.LoadFactor = MathF.Min(targetFactor, compute.LoadFactor + step);
            else
                compute.LoadFactor = MathF.Max(targetFactor, compute.LoadFactor - step);
        }

        if (MathF.Abs(prevFactor - compute.LoadFactor) > 0.0001f)
            Dirty(uid, compute);

        var load = MathHelper.Lerp(compute.IdlePowerLoad, compute.ActivePowerLoad, compute.LoadFactor);
        if (Math.Abs(power.Load - load) > 0.1f)
            _apcPower.SetLoad(power, load);
    }

    private void RefreshActiveComputeGrids()
    {
        _activeComputeGrids.Clear();

        var sources = EntityQueryEnumerator<DiscoverySourceConsoleComponent, TransformComponent>();
        while (sources.MoveNext(out _, out var source, out var xform))
        {
            if (source.Scanning && !source.AutoScanning && xform.GridUid is { } grid)
                _activeComputeGrids.Add(grid);
        }

        var analysis = EntityQueryEnumerator<DiscoveryAnalysisConsoleComponent, TransformComponent>();
        while (analysis.MoveNext(out _, out var console, out var xform))
        {
            if (console.Decoding && xform.GridUid is { } grid)
                _activeComputeGrids.Add(grid);
        }
    }

    private void CollectComputeEnvironments(EntityUid uid, TransformComponent xform)
    {
        _computeHeatEnvironments.Clear();
        var position = _transform.GetGridTilePositionOrDefault((uid, xform));

        if (_atmosphere.GetTileMixture(xform.GridUid, xform.MapUid, position, true) is { } tileMix)
            _computeHeatEnvironments.Add(tileMix);

        if (xform.GridUid == null)
            return;

        var enumerator = _atmosphere.GetAdjacentTileMixtures(xform.GridUid.Value, position, false, true);
        while (enumerator.MoveNext(out var mix))
            _computeHeatEnvironments.Add(mix);
    }

    private static bool IsComputeEnvironmentUnsafe(List<GasMixture> environments)
    {
        if (environments.Count == 0)
            return true;

        var avgTemp = 0f;
        foreach (var env in environments)
            avgTemp += env.Temperature;
        avgTemp /= environments.Count;

        return avgTemp >= DiscoveryResearchConstants.ComputeMaxOperatingTemperature ||
               avgTemp <= DiscoveryResearchConstants.ComputeMinOperatingTemperature;
    }

    private void SetComputeThermalOffline(EntityUid uid, ResearchComputeServerComponent compute, bool offline)
    {
        if (compute.ThermalOffline == offline)
        {
            if (offline && !compute.Broken)
                _powerReceiver.SetPowerDisabled(uid, true);
            return;
        }

        compute.ThermalOffline = offline;
        Dirty(uid, compute);

        if (!compute.Broken)
            _powerReceiver.SetPowerDisabled(uid, offline);

        UpdateComputeAppearance(uid, compute);
        RefreshComputeUi(uid, compute);
    }

    private void RefreshComputeUi(EntityUid uid, ResearchComputeServerComponent compute)
    {
        if (!_ui.IsUiOpen(uid, ResearchComputeServerUiKey.Key))
            return;
        _ui.SetUiState(uid, ResearchComputeServerUiKey.Key, BuildComputeState(uid, compute));
    }

    private ResearchComputeServerBoundUserInterfaceState BuildComputeState(
        EntityUid uid,
        ResearchComputeServerComponent compute)
    {
        CollectComputeEnvironments(uid, Transform(uid));
        float? tempC = null;
        if (_computeHeatEnvironments.Count > 0)
        {
            var avgK = 0f;
            foreach (var env in _computeHeatEnvironments)
                avgK += env.Temperature;
            avgK /= _computeHeatEnvironments.Count;
            tempC = avgK - Atmospherics.T0C;
        }

        var powered = TryComp(uid, out ApcPowerReceiverComponent? power) && power.Powered;
        var switchOn = power is { PowerDisabled: false };
        var underLoad = powered &&
                        !compute.Broken &&
                        !compute.ThermalOffline &&
                        compute.LoadFactor > 0.01f;

        var loadWatts = 0;
        if (power != null)
            loadWatts = (int)power.Load;

        return new ResearchComputeServerBoundUserInterfaceState
        {
            Name = MetaData(uid).EntityName,
            Powered = powered,
            PowerSwitchOn = switchOn,
            Broken = compute.Broken,
            ThermalOffline = compute.ThermalOffline,
            UnderLoad = underLoad,
            TemperatureCelsius = tempC,
            PowerLoadWatts = loadWatts,
            HeatWatts = (int)(compute.HeatEnergyPerSecond * compute.LoadFactor),
            WorkingOnGrid = CountPoweredWorkingCompute(uid),
            LinkedOnGrid = CountLinkedComputeOnGrid(uid),
            Processes = BuildComputeProcesses(uid),
        };
    }

    private void UpdateComputeUiRefresh(float frameTime)
    {
        _computeUiRefreshAccum += frameTime;
        if (_computeUiRefreshAccum < 0.5f)
            return;

        _computeUiRefreshAccum = 0f;
        var query = EntityQueryEnumerator<ResearchComputeServerComponent>();
        while (query.MoveNext(out var uid, out var compute))
            RefreshComputeUi(uid, compute);
    }
    private List<ResearchComputeProcessEntry> BuildComputeProcesses(EntityUid rackUid)
    {
        var list = new List<ResearchComputeProcessEntry>();
        if (!TryComp(rackUid, out TransformComponent? xform) || xform.GridUid is not { } grid)
            return list;

        const int maxEntries = 14;
        var pid = 1000;
        var jobs = new List<ResearchComputeProcessEntry>();

        var sources = EntityQueryEnumerator<DiscoverySourceConsoleComponent, TransformComponent>();
        while (sources.MoveNext(out var sourceUid, out var source, out var stx))
        {
            if (stx.GridUid != grid || !source.Scanning)
                continue;

            var progress = source.ScanDuration > 0.01f
                ? Math.Clamp(source.ScanProgress / source.ScanDuration, 0f, 1f)
                : 0f;

            jobs.Add(new ResearchComputeProcessEntry
            {
                Pid = pid++,
                Name = TruncateName(MetaData(sourceUid).EntityName),
                Kind = source.AutoScanning
                    ? ResearchComputeProcessKind.AutoScan
                    : ResearchComputeProcessKind.Scan,
                Status = ResearchComputeProcessStatus.Run,
                Progress = progress,
            });
        }

        var analysis = EntityQueryEnumerator<DiscoveryAnalysisConsoleComponent, TransformComponent>();
        while (analysis.MoveNext(out var analysisUid, out var console, out var atx))
        {
            if (atx.GridUid != grid || !console.Decoding)
                continue;

            var progress = 0f;
            if (TryGetDisk(analysisUid, console.DiskSlotId, out _, out var disk) &&
                disk.LockedTechId != null &&
                PrototypeManager.TryIndex(disk.LockedTechId.Value, out TechnologyPrototype? tech))
            {
                var unlocked = 0;
                if (TryGetClientServer(analysisUid, out _, out _, out var db))
                    unlocked = db.UnlockedTechnologies.Count;

                var duration = DecodeDurationSeconds(tech.Cost, unlocked, GetEffectiveComputeLoad(analysisUid));
                if (duration > 0.01f)
                    progress = Math.Clamp(disk.DecodeProgress / duration, 0f, 1f);
            }

            jobs.Add(new ResearchComputeProcessEntry
            {
                Pid = pid++,
                Name = TruncateName(MetaData(analysisUid).EntityName),
                Kind = ResearchComputeProcessKind.Decode,
                Status = ResearchComputeProcessStatus.Run,
                Progress = progress,
            });
        }

        var journals = EntityQueryEnumerator<DiscoveryJournalConsoleComponent, TransformComponent>();
        while (journals.MoveNext(out var journalUid, out var journal, out var jtx))
        {
            if (jtx.GridUid != grid || !journal.Printing)
                continue;

            var progress = journal.PrintDuration > 0.01f
                ? Math.Clamp(journal.PrintProgress / journal.PrintDuration, 0f, 1f)
                : 0f;

            jobs.Add(new ResearchComputeProcessEntry
            {
                Pid = pid++,
                Name = TruncateName(MetaData(journalUid).EntityName),
                Kind = ResearchComputeProcessKind.Print,
                Status = ResearchComputeProcessStatus.Run,
                Progress = progress,
            });
        }

        jobs.Sort((a, b) => b.Progress.CompareTo(a.Progress));
        foreach (var job in jobs)
        {
            if (list.Count >= maxEntries)
                return list;
            list.Add(job);
        }
        var racks = EntityQueryEnumerator<ResearchComputeServerComponent, TransformComponent, ApcPowerReceiverComponent>();
        while (racks.MoveNext(out var otherUid, out var otherCompute, out var otx, out var otherPower))
        {
            if (otx.GridUid != grid)
                continue;

            if (list.Count >= maxEntries)
                break;

            ResearchComputeProcessStatus status;
            float progress;
            if (otherCompute.Broken)
            {
                status = ResearchComputeProcessStatus.Broken;
                progress = 0f;
            }
            else if (otherCompute.ThermalOffline)
            {
                status = ResearchComputeProcessStatus.Thermal;
                progress = 0f;
            }
            else if (otherPower.PowerDisabled || !otherPower.Powered)
            {
                status = ResearchComputeProcessStatus.Off;
                progress = 0f;
            }
            else if (otherCompute.LoadFactor > 0.01f)
            {
                status = ResearchComputeProcessStatus.Load;
                progress = otherCompute.LoadFactor;
            }
            else
            {
                status = ResearchComputeProcessStatus.Idle;
                progress = 0f;
            }

            var name = TruncateName(MetaData(otherUid).EntityName);
            if (otherUid == rackUid)
                name = Loc.GetString("research-compute-server-ui-proc-self", ("name", name));

            list.Add(new ResearchComputeProcessEntry
            {
                Pid = pid++,
                Name = name,
                Kind = ResearchComputeProcessKind.Rack,
                Status = status,
                Progress = progress,
            });
        }

        return list;
    }

    private static string TruncateName(string name)
    {
        const int max = 28;
        if (string.IsNullOrEmpty(name))
            return "-";
        return name.Length <= max ? name : name[..(max - 1)] + "…";
    }

    private void UpdateComputeFailures(float frameTime)
    {
        var query = EntityQueryEnumerator<ResearchComputeServerComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var compute, out var power))
        {
            if (compute.Broken || compute.ThermalOffline || !power.Powered)
                continue;

            compute.FailureAccumulator += frameTime;
            if (compute.FailureAccumulator < compute.FailureCheckInterval)
                continue;

            compute.FailureAccumulator = 0;
            var chance = compute.FailureCheckInterval /
                         (float)DiscoveryResearchConstants.ComputeMeanTimeBetweenFailures.TotalSeconds;
            if (!Random.Prob(chance))
                continue;

            SetComputeBroken(uid, compute);
        }
    }

    private void UpdateComputeSparks(float frameTime)
    {
        var interval = (float)DiscoveryResearchConstants.ComputeSparkInterval.TotalSeconds;
        var query = EntityQueryEnumerator<ResearchComputeServerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var compute, out var xform))
        {
            if (!compute.Broken)
                continue;

            compute.SparkAccumulator += frameTime;
            if (compute.SparkAccumulator < interval)
                continue;

            compute.SparkAccumulator = 0f;
            if (Random.Prob(0.65f))
            {
                Spawn(SparksEffectProto, xform.Coordinates);
                _audio.PlayPvs(ComputeSparkSound, uid, ComputeSparkAudioParams);
            }
        }
    }

    private int CountPoweredWorkingCompute(EntityUid machine)
    {
        if (!TryComp(machine, out TransformComponent? xform) || xform.GridUid == null)
            return 0;

        var grid = xform.GridUid.Value;
        var count = 0;
        var query = EntityQueryEnumerator<ResearchComputeServerComponent, TransformComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out _, out var compute, out var tx, out var power))
        {
            if (compute.Broken || compute.ThermalOffline || !power.Powered)
                continue;
            if (tx.GridUid != grid)
                continue;
            count++;
        }
        return count;
    }

    private float GetEffectiveComputeLoad(EntityUid machine)
    {
        if (!TryComp(machine, out TransformComponent? xform) || xform.GridUid == null)
            return 0f;

        var grid = xform.GridUid.Value;
        var load = 0f;
        var query = EntityQueryEnumerator<ResearchComputeServerComponent, TransformComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out _, out var compute, out var tx, out var power))
        {
            if (compute.Broken || compute.ThermalOffline || !power.Powered)
                continue;
            if (tx.GridUid != grid)
                continue;
            load += compute.LoadFactor;
        }
        return load;
    }

    private bool TryGetDisk(EntityUid machine, string slotId, out EntityUid diskUid, out ResearchDataDiskComponent disk)
    {
        diskUid = default;
        disk = null!;
        if (!_slots.TryGetSlot(machine, slotId, out var slot) || slot.Item is not { } item)
            return false;
        if (!TryComp(item, out ResearchDataDiskComponent? diskComp))
            return false;
        diskUid = item;
        disk = diskComp;
        return true;
    }

    private bool TryGetClientServer(EntityUid client, out EntityUid server, out ResearchServerComponent serverComp, out TechnologyDatabaseComponent database)
    {
        server = default;
        serverComp = null!;
        database = null!;
        if (!TryComp<ResearchClientComponent>(client, out var researchClient))
            return false;
        if (!_research.TryGetClientServer(client, out var serverUid, out var sComp, researchClient) || serverUid == null || sComp == null)
            return false;
        if (!TryComp(serverUid.Value, out TechnologyDatabaseComponent? db))
            return false;
        server = serverUid.Value;
        serverComp = sComp;
        database = db;
        return true;
    }

    private void TryEjectDisk(EntityUid machine, string slotId, EntityUid user)
    {
        if (!_slots.TryGetSlot(machine, slotId, out var slot) || slot.Item == null)
            return;
        _slots.TryEject(machine, slotId, user, out _);
    }
}
