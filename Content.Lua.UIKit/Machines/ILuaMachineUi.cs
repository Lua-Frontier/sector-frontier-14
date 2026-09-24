// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared._NF.Lathe;
using Content.Shared._NF.Research;
using Content.Shared._NF.Research.Prototypes;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Lua.UIKit.Machines;

public interface ILunaLatheMenu
{
    BaseWindow Window { get; }
    List<ProtoId<LatheRecipePrototype>> Recipes { get; set; }
    event Action<BaseButton.ButtonEventArgs>? OnServerListButtonPressed;
    event Action<string, int>? RecipeQueueAction;
    event Action<int>? QueueDeleteAction;
    event Action<int>? QueueMoveUpAction;
    event Action<int>? QueueMoveDownAction;
    event Action? DeleteFabricatingAction;
    event Action<bool>? OnLoopCheckboxPressed;
    event Action<bool>? OnSkipCheckboxPressed;
    void SetEntity(EntityUid uid);
    void Refresh();
    void PopulateQueueList(List<LatheRecipeBatch> queue);
    void SetQueueInfo(
        ProtoId<LatheRecipePrototype>? recipeProto,
        TimeSpan? productionStartedAt = null,
        TimeSpan? productionLength = null);
    void SetLooping(bool loop);
    void SetSkipping(bool skip);
    void SetResearchServerConnected(bool connected);
}

public interface ILunaBlueprintLatheMenu
{
    BaseWindow Window { get; }
    Dictionary<ProtoId<BlueprintPrototype>, int[]> RecipesByBlueprintType { get; set; }
    event Action<BaseButton.ButtonEventArgs>? OnServerListButtonPressed;
    event Action<ProtoId<BlueprintPrototype>, int[], int>? RecipeQueueAction;
    event Action<int>? QueueDeleteAction;
    event Action<int>? QueueMoveUpAction;
    event Action<int>? QueueMoveDownAction;
    event Action? DeleteFabricatingAction;
    void SetEntity(EntityUid uid);
    void UpdateCategories();
    void PopulateQueueList(List<BlueprintLatheRecipeBatch> queue);
    void SetQueueInfo(ProtoId<BlueprintPrototype>? recipe);
    void SetResearchServerConnected(bool connected);
}

public interface ILuaVendingMachineWindow
{
    BaseWindow Window { get; }
    string? Title { get; set; }
    event Action<InventoryType, string>? OnItemSelected;
    event Action? OnClose;
    void Populate(List<VendingMachineInventoryEntry> inventory, bool enabled, float priceModifier, int balance, int? cashSlotBalance);
    void UpdateBalance(int balance);
    void UpdateCashSlotBalance(int? balance);
    void UpdateAmounts(List<VendingMachineInventoryEntry> cachedInventory, float priceModifier, bool enabled);
    void Dispose();
}

public interface ILunaResearchConsoleMenu
{
    BaseWindow Window { get; }
    Dictionary<string, ResearchAvailability> List { get; }
    Action<string>? OnTechnologyCardPressed { get; set; }
    Action<string>? OnProjectCancelPressed { get; set; }
    Action? OnServerButtonPressed { get; set; }
    event Action? OnClose;
    void SetEntity(EntityUid entity);
    void SetResearchFaction(string? faction);
    void UpdateQueue(
        List<ResearchProjectUiState> active,
        List<ResearchProjectUiState> queued,
        int maxActiveSlots,
        Dictionary<string, int>? technologyProgress = null);
    void UpdatePanels(Dictionary<string, ResearchAvailability> dict);
    void SyncProjectVisuals();
    void UpdateInformationPanel(int points, bool rebuildTiers = true);
}

public interface IShipyardDockRadar
{
    Control Control { get; }
    bool ShowIFF { get; set; }
    bool ShowIFFShuttles { get; set; }
    bool ShowDocks { get; set; }
    NetEntity? HighlightDockPort { get; set; }
    Action<EntityCoordinates>? OnRadarClick { get; set; }
    void SetConsole(EntityUid? console);
    void UpdateState(NavInterfaceState state);
}

public interface ILuaMachineUiFactory
{
    ILunaLatheMenu CreateLatheMenu();
    ILunaBlueprintLatheMenu CreateBlueprintLatheMenu();
    ILuaVendingMachineWindow CreateVendingMachineWindow();
    ILunaResearchConsoleMenu CreateResearchConsoleMenu();
    IShipyardDockRadar CreateShipyardDockRadar();
}
