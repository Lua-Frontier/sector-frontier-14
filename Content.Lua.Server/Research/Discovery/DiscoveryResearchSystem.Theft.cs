using System.Linq;
using Content.Lua.Shared.Research.Discovery;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Content.Shared.Research.Discovery;

namespace Content.Lua.Server.Research.Discovery;

public sealed partial class DiscoveryResearchSystem
{
    private void OnTheftAfterInteract(EntityUid uid, ServerTechReconfiguratorComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        if (TryComp(target, out ResearchComputeServerComponent? compute) && compute.Broken)
        {
            args.Handled = true;
            StartComputeRepair(uid, comp, target, args.User);
            return;
        }

        if (!HasComp<ResearchServerComponent>(target) || !HasComp<TechnologyDatabaseComponent>(target))
            return;

        args.Handled = true;
        if (!_powerReceiver.IsPowered(target))
        {
            _popup.PopupEntity(Loc.GetString("server-tech-reconfigurator-server-unpowered"), target, args.User);
            return;
        }

        ResetTheftSession(comp, clearTarget: false);
        comp.TargetServer = target;
        comp.User = args.User;
        if (!_ui.TryOpenUi(uid, ServerTechReconfiguratorUiKey.Key, args.User))
        {
            ResetTheftSession(comp);
            return;
        }

        RefreshTheftUi(uid, comp);
    }

    private void StartComputeRepair(
        EntityUid device,
        ServerTechReconfiguratorComponent comp,
        EntityUid target,
        EntityUid user)
    {
        ResetTheftSession(comp, clearTarget: false);
        comp.RepairMode = true;
        comp.TargetServer = target;
        comp.User = user;
        comp.Downloading = false;
        comp.DownloadProgress = 0f;
        comp.SelectedTechnology = null;
        GeneratePuzzle(comp);
        SeedBrokenRepairLinks(comp);

        if (!_ui.TryOpenUi(device, ServerTechReconfiguratorUiKey.Key, user))
        {
            ResetTheftSession(comp);
            return;
        }

        RefreshTheftUi(device, comp);
    }

    private void OnTheftUiClosed(EntityUid uid, ServerTechReconfiguratorComponent comp, BoundUIClosedEvent args)
    {
        if (!args.UiKey.Equals(ServerTechReconfiguratorUiKey.Key))
            return;
        ResetTheftSession(comp);
    }

    private void OnTheftDiskInserted(EntityUid uid, ServerTechReconfiguratorComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == comp.DiskSlotId)
            RefreshTheftUi(uid, comp);
    }

    private void OnTheftDiskRemoved(EntityUid uid, ServerTechReconfiguratorComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != comp.DiskSlotId)
            return;
        if (comp.RepairMode)
            return;
        ResetPuzzle(comp);
        RefreshTheftUi(uid, comp);
    }

    private void OnTheftSelect(EntityUid uid, ServerTechReconfiguratorComponent comp, ServerTechSelectTechnologyMessage args)
    {
        if (comp.RepairMode)
            return;
        if (!ValidateTheftSession(uid, comp, args.Actor, requireEmptyDisk: false, out _, out var database))
            return;
        if (!database.UnlockedTechnologies.Contains(args.TechnologyId))
            return;
        comp.SelectedTechnology = args.TechnologyId;
        ResetPuzzle(comp);
        RefreshTheftUi(uid, comp);
    }

    private void OnTheftStart(EntityUid uid, ServerTechReconfiguratorComponent comp, ServerTechStartTheftMessage args)
    {
        if (comp.RepairMode)
            return;
        if (!ValidateTheftSession(uid, comp, args.Actor, requireEmptyDisk: true, out _, out var database))
            return;
        if (comp.SelectedTechnology == null || !database.UnlockedTechnologies.Contains(comp.SelectedTechnology))
            return;

        comp.Downloading = false;
        comp.DownloadProgress = 0f;
        GeneratePuzzle(comp);
        RefreshTheftUi(uid, comp);
    }

    private void OnTheftSubmitRoute(EntityUid uid, ServerTechReconfiguratorComponent comp, ServerTechSubmitRouteMessage args)
    {
        if (comp.RepairMode)
        {
            OnRepairSubmitRoute(uid, comp, args);
            return;
        }

        if (comp.Pairs.Count == 0 ||
            !ValidateTheftSession(uid, comp, args.Actor, requireEmptyDisk: true, out _, out _))
            return;
        if (comp.AcceptedRoutes.ContainsKey(args.PairId) ||
            !ValidateRoute(comp, args.PairId, args.Cells))
        {
            _popup.PopupEntity(Loc.GetString("server-tech-reconfigurator-invalid-route"), uid, args.Actor);
            return;
        }

        comp.AcceptedRoutes[args.PairId] = args.Cells.ToList();
        if (comp.AcceptedRoutes.Count < comp.Pairs.Count)
        {
            RefreshTheftUi(uid, comp);
            return;
        }

        comp.Pairs.Clear();
        comp.AcceptedRoutes.Clear();
        comp.Downloading = true;
        comp.DownloadProgress = 0f;
        RefreshTheftUi(uid, comp);
    }

    private void OnRepairSubmitRoute(
        EntityUid uid,
        ServerTechReconfiguratorComponent comp,
        ServerTechSubmitRouteMessage args)
    {
        if (comp.Pairs.Count == 0 || !ValidateRepairSession(uid, comp, args.Actor, out _))
            return;

        if (comp.AcceptedRoutes.ContainsKey(args.PairId) ||
            !ValidateRoute(comp, args.PairId, args.Cells))
        {
            _popup.PopupEntity(Loc.GetString("server-tech-reconfigurator-invalid-route"), uid, args.Actor);
            return;
        }

        comp.AcceptedRoutes[args.PairId] = args.Cells.ToList();
        if (comp.AcceptedRoutes.Count < comp.Pairs.Count)
        {
            RefreshTheftUi(uid, comp);
            return;
        }

        CompleteComputeRepair(uid, comp, args.Actor);
    }

    private void OnTheftResetPuzzle(EntityUid uid, ServerTechReconfiguratorComponent comp, ServerTechResetPuzzleMessage args)
    {
        if (comp.RepairMode)
        {
            if (!ValidateRepairSession(uid, comp, args.Actor, out _))
                return;
        }
        else if (!ValidateTheftSession(uid, comp, args.Actor, requireEmptyDisk: false, out _, out _))
        {
            return;
        }

        if (comp.Pairs.Count > 0)
        {
            if (comp.RepairMode)
                RestoreIntactRepairRoutes(comp);
            else
                comp.AcceptedRoutes.Clear();
            RefreshTheftUi(uid, comp);
        }
    }

    private void OnTheftEject(EntityUid uid, ServerTechReconfiguratorComponent comp, ServerTechEjectDiskMessage args)
    {
        if (comp.RepairMode)
            return;
        ResetPuzzle(comp);
        TryEjectDisk(uid, comp.DiskSlotId, args.Actor);
        RefreshTheftUi(uid, comp);
    }

    private void UpdateTheftConnections()
    {
        var query = EntityQueryEnumerator<ServerTechReconfiguratorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.User is not { } actor || comp.TargetServer is not { } server)
                continue;

            if (comp.RepairMode)
            {
                if (TryComp(server, out ResearchComputeServerComponent? compute) &&
                    compute.Broken &&
                    _interactionSystem.InRangeUnobstructed(actor, server, 2f))
                    continue;
                InvalidateTheftSession(uid, comp, actor);
                continue;
            }

            if (TryComp(server, out TechnologyDatabaseComponent? _) &&
                HasComp<ResearchServerComponent>(server) &&
                _powerReceiver.IsPowered(server) &&
                _interactionSystem.InRangeUnobstructed(actor, server, 2f))
                continue;
            InvalidateTheftSession(uid, comp, actor);
        }
    }

    private void UpdateTheftDownloads(float frameTime)
    {
        var query = EntityQueryEnumerator<ServerTechReconfiguratorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.RepairMode || !comp.Downloading || comp.User is not { } actor)
                continue;
            if (!ValidateTheftSession(uid, comp, actor, requireEmptyDisk: true, out _, out var database) ||
                comp.SelectedTechnology == null ||
                !database.UnlockedTechnologies.Contains(comp.SelectedTechnology))
            {
                ResetPuzzle(comp);
                RefreshTheftUi(uid, comp);
                continue;
            }

            comp.DownloadProgress += frameTime;
            if (comp.DownloadProgress >= comp.DownloadDuration)
            {
                CompleteTheft(uid, comp, actor);
                continue;
            }

            if (_ui.IsUiOpen(uid, ServerTechReconfiguratorUiKey.Key))
                RefreshTheftUi(uid, comp);
        }
    }

    private bool ValidateTheftSession(
        EntityUid device,
        ServerTechReconfiguratorComponent comp,
        EntityUid actor,
        bool requireEmptyDisk,
        out EntityUid server,
        out TechnologyDatabaseComponent database)
    {
        server = default;
        database = null!;
        if (comp.User != actor || comp.TargetServer is not { } target)
        {
            InvalidateTheftSession(device, comp, actor);
            return false;
        }

        if (!TryComp(target, out TechnologyDatabaseComponent? db) ||
            !HasComp<ResearchServerComponent>(target) ||
            !_interactionSystem.InRangeUnobstructed(actor, target, 2f) ||
            !_powerReceiver.IsPowered(target))
        {
            InvalidateTheftSession(device, comp, actor);
            return false;
        }

        if (requireEmptyDisk &&
            (!TryGetDisk(device, comp.DiskSlotId, out _, out var disk) ||
             disk.Stage != ResearchDataDiskStage.Empty))
        {
            ResetPuzzle(comp);
            _popup.PopupEntity(Loc.GetString("server-tech-reconfigurator-need-empty-disk"), device, actor);
            RefreshTheftUi(device, comp);
            return false;
        }

        server = target;
        database = db;
        return true;
    }

    private bool ValidateRepairSession(
        EntityUid device,
        ServerTechReconfiguratorComponent comp,
        EntityUid actor,
        out EntityUid server)
    {
        server = default;
        if (comp.User != actor || comp.TargetServer is not { } target)
        {
            InvalidateTheftSession(device, comp, actor);
            return false;
        }

        if (!TryComp(target, out ResearchComputeServerComponent? compute) ||
            !compute.Broken ||
            !_interactionSystem.InRangeUnobstructed(actor, target, 2f))
        {
            InvalidateTheftSession(device, comp, actor);
            return false;
        }

        server = target;
        return true;
    }

    private void InvalidateTheftSession(EntityUid device, ServerTechReconfiguratorComponent comp, EntityUid actor)
    {
        ResetTheftSession(comp);
        _ui.CloseUi(device, ServerTechReconfiguratorUiKey.Key, actor);
        _popup.PopupEntity(Loc.GetString("server-tech-reconfigurator-connection-lost"), device, actor);
    }

    private void GeneratePuzzle(ServerTechReconfiguratorComponent comp, int? pairCount = null, int? gridSize = null)
    {
        var size = gridSize ?? (comp.SessionGridSize > 0 ? comp.SessionGridSize : comp.GridSize);
        var count = pairCount ?? comp.PairCount;
        comp.SessionGridSize = size;
        comp.AcceptedRoutes.Clear();
        comp.Pairs.Clear();
        var leftRows = Enumerable.Range(0, size).ToList();
        var rightRows = Enumerable.Range(0, size).ToList();
        Random.Shuffle(leftRows);
        Random.Shuffle(rightRows);

        count = Math.Min(count, size);
        for (var i = 0; i < count; i++)
        {
            comp.Pairs.Add(new ServerTechRoutePair(
                i,
                new Vector2i(0, leftRows[i]),
                new Vector2i(size - 1, rightRows[i])));
        }
    }

    private void SeedBrokenRepairLinks(ServerTechReconfiguratorComponent comp)
    {
        comp.BrokenRepairPairs.Clear();
        if (comp.Pairs.Count == 0)
            return;

        var brokenCount = Math.Clamp(Random.Next(1, 6), 1, comp.Pairs.Count);
        var ids = comp.Pairs.Select(pair => pair.Id).ToList();
        Random.Shuffle(ids);
        for (var i = 0; i < brokenCount; i++)
            comp.BrokenRepairPairs.Add(ids[i]);

        RestoreIntactRepairRoutes(comp);
    }

    private static void RestoreIntactRepairRoutes(ServerTechReconfiguratorComponent comp)
    {
        comp.AcceptedRoutes.Clear();
        foreach (var pair in comp.Pairs)
        {
            if (comp.BrokenRepairPairs.Contains(pair.Id))
                continue;
            comp.AcceptedRoutes[pair.Id] = new List<Vector2i> { pair.Start, pair.End };
        }
    }

    private static bool ValidateRoute(ServerTechReconfiguratorComponent comp, int pairId, List<Vector2i> cells)
    {
        var pair = comp.Pairs.FirstOrDefault(candidate => candidate.Id == pairId);
        if (pair == null || cells.Count != 2)
            return false;
        return (cells[0] == pair.Start && cells[1] == pair.End) ||
               (cells[0] == pair.End && cells[1] == pair.Start);
    }

    private void CompleteComputeRepair(EntityUid device, ServerTechReconfiguratorComponent comp, EntityUid actor)
    {
        if (!ValidateRepairSession(device, comp, actor, out var target) ||
            !TryComp(target, out ResearchComputeServerComponent? compute))
            return;

        compute.Broken = false;
        Dirty(target, compute);
        if (!compute.ThermalOffline)
            _powerReceiver.SetPowerDisabled(target, false);
        UpdateComputeAppearance(target, compute);
        _popup.PopupEntity(Loc.GetString("discovery-research-compute-repaired"), target, actor);

        ResetTheftSession(comp);
        _ui.CloseUi(device, ServerTechReconfiguratorUiKey.Key, actor);
    }

    private void CompleteTheft(EntityUid device, ServerTechReconfiguratorComponent comp, EntityUid actor)
    {
        if (!ValidateTheftSession(device, comp, actor, requireEmptyDisk: true, out var server, out var database) ||
            comp.SelectedTechnology == null ||
            !PrototypeManager.TryIndex(comp.SelectedTechnology, out TechnologyPrototype? tech) ||
            !database.UnlockedTechnologies.Contains(tech.ID) ||
            !TryGetDisk(device, comp.DiskSlotId, out var diskUid, out var disk))
            return;

        if (!_research.TryRemoveTechnology((server, database), tech))
        {
            ResetPuzzle(comp);
            RefreshTheftUi(device, comp);
            return;
        }

        disk.Stage = ResearchDataDiskStage.Decoded;
        disk.LockedTechId = tech.ID;
        disk.LeadDiscipline = tech.Discipline;
        disk.LeadTierHint = 0;
        disk.LeadTags = tech.Tags.ToList();
        disk.LeadRisk = tech.Cost >= 15000 ? "high" : tech.Cost >= 5000 ? "medium" : "low";
        disk.LeadCostEstimate = tech.Cost;
        disk.DecodeCost = tech.Cost;
        disk.DecodeProgress = 0f;
        Dirty(diskUid, disk);
        UpdateDiskAppearance(diskUid, disk);

        if (TryComp(server, out ResearchServerComponent? researchServer))
        {
            foreach (var client in researchServer.Clients.ToList())
                _research.SyncClientWithServer(client);
        }

        _popup.PopupEntity(
            Loc.GetString("server-tech-reconfigurator-success", ("technology", TechnologyDisplayName(tech))),
            device,
            actor);
        comp.SelectedTechnology = null;
        ResetPuzzle(comp);
        RefreshTheftUi(device, comp);
    }

    private void RefreshTheftUi(EntityUid uid, ServerTechReconfiguratorComponent comp)
    {
        _ui.SetUiState(uid, ServerTechReconfiguratorUiKey.Key, BuildTheftState(uid, comp));
    }

    private ServerTechReconfiguratorBoundUserInterfaceState BuildTheftState(
        EntityUid uid,
        ServerTechReconfiguratorComponent comp)
    {
        var gridSize = comp.SessionGridSize > 0 ? comp.SessionGridSize : comp.GridSize;
        var state = new ServerTechReconfiguratorBoundUserInterfaceState
        {
            SelectedTechnology = comp.SelectedTechnology,
            PuzzleActive = comp.Pairs.Count > 0,
            Downloading = comp.Downloading,
            DownloadProgress = comp.DownloadProgress,
            DownloadDuration = comp.DownloadDuration,
            GridSize = gridSize,
            IsRepairMode = comp.RepairMode,
            Pairs = comp.Pairs.ToList(),
            AcceptedRoutes = comp.AcceptedRoutes
                .Select(route => new ServerTechAcceptedRoute(route.Key, route.Value.ToList()))
                .ToList(),
        };

        if (TryGetDisk(uid, comp.DiskSlotId, out _, out var disk))
        {
            state.HasDisk = true;
            state.DiskStage = disk.Stage;
        }

        if (comp.RepairMode)
        {
            if (comp.TargetServer is { } repairTarget &&
                HasComp<ResearchComputeServerComponent>(repairTarget))
            {
                state.HasTarget = true;
                state.TargetName = MetaData(repairTarget).EntityName;
                state.TargetPowered = false;
            }

            return state;
        }

        if (comp.TargetServer is not { } server ||
            !TryComp(server, out TechnologyDatabaseComponent? database) ||
            !TryComp(server, out ResearchServerComponent? researchServer))
            return state;

        state.HasTarget = true;
        state.TargetName = MetaData(server).EntityName;
        state.TargetPowered = _powerReceiver.IsPowered(server);
        FactionResearchProfilePrototype? profile = null;
        PrototypeManager.TryIndex(
            new ProtoId<FactionResearchProfilePrototype>((string) researchServer.Faction),
            out profile);

        foreach (var techId in database.UnlockedTechnologies)
        {
            if (!PrototypeManager.TryIndex(techId, out TechnologyPrototype? tech))
                continue;

            var disciplineName = (string) tech.Discipline;
            var disciplineColor = Color.FromHex("#7B68A6");
            if (PrototypeManager.TryIndex(tech.Discipline, out TechDisciplinePrototype? discipline))
            {
                disciplineName = Loc.GetString(discipline.Name);
                disciplineColor = discipline.Color;
            }

            string? entityIcon = null;
            if (tech.EntityIcon is { } icon)
            {
                if (PrototypeManager.HasIndex(icon))
                    entityIcon = icon;
                else if (PrototypeManager.TryIndex<LatheRecipePrototype>(icon.Id, out var iconRecipe) &&
                         iconRecipe.Result is { } iconResult)
                    entityIcon = iconResult;
            }

            if (entityIcon == null)
            {
                foreach (var unlock in tech.RecipeUnlocks)
                {
                    if (!PrototypeManager.TryIndex(unlock, out LatheRecipePrototype? recipe) ||
                        recipe.Result is not { } result)
                        continue;
                    entityIcon = result;
                    break;
                }
            }

            var recipeNames = new List<string>();
            foreach (var recipeId in tech.RecipeUnlocks)
                recipeNames.Add(RecipeDisplayName(recipeId));

            state.Technologies.Add(new DiscoveryUnlockedEntry
            {
                Id = tech.ID,
                Name = TechnologyDisplayName(tech),
                Discipline = tech.Discipline,
                DisciplineName = disciplineName,
                DisciplineColor = disciplineColor,
                EntityIcon = entityIcon,
                Cost = tech.Cost,
                Tier = 0,
                RecipeIds = tech.RecipeUnlocks.Select(recipe => (string) recipe).ToList(),
                RecipeNames = recipeNames,
                IsSignature = profile != null &&
                              (profile.SignatureTechs.Contains(tech.ID) || tech.SignatureFor == researchServer.Faction),
            });
        }

        state.Technologies = state.Technologies
            .OrderBy(entry => entry.Discipline)
            .ThenBy(entry => entry.Name)
            .ToList();
        return state;
    }

    private static void ResetPuzzle(ServerTechReconfiguratorComponent comp)
    {
        comp.Downloading = false;
        comp.DownloadProgress = 0f;
        comp.Pairs.Clear();
        comp.AcceptedRoutes.Clear();
    }

    private static void ResetTheftSession(ServerTechReconfiguratorComponent comp, bool clearTarget = true)
    {
        ResetPuzzle(comp);
        comp.SelectedTechnology = null;
        comp.RepairMode = false;
        comp.SessionGridSize = 0;
        comp.BrokenRepairPairs.Clear();
        if (!clearTarget)
            return;
        comp.TargetServer = null;
        comp.User = null;
    }
}
