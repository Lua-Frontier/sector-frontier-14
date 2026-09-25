using Content.Client.UserInterface;
using Content.Lua.UIKit.Machines;
using Content.Shared._NF.Lathe;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._NF.Lathe.UI;

[UsedImplicitly]
public sealed class BlueprintLatheNFBoundUserInterface : FactoryBoundUserInterface
{
    [ViewVariables]
    private ILunaBlueprintLatheMenu? _menu;

    public BlueprintLatheNFBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        if (!IoCManager.Instance!.TryResolveType<ILuaMachineUiFactory>(out var factory))
            return;

        _menu = factory.CreateBlueprintLatheMenu();
        OpenWindowCenteredRight(_menu.Window);
        _menu.SetEntity(Owner);

        _menu.OnServerListButtonPressed += _ =>
        {
            SendMessage(new ConsoleServerSelectionMessage());
        };

        _menu.RecipeQueueAction += (blueprintType, recipes, amount) =>
        {
            SendMessage(new BlueprintLatheQueueRecipeMessage(blueprintType, recipes, amount));
        };

        _menu.QueueDeleteAction += index => SendMessage(new LatheDeleteRequestMessage(index));
        _menu.QueueMoveUpAction += index => SendMessage(new LatheMoveRequestMessage(index, -1));
        _menu.QueueMoveDownAction += index => SendMessage(new LatheMoveRequestMessage(index, 1));
        _menu.DeleteFabricatingAction += () => SendMessage(new LatheAbortFabricationMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not BlueprintLatheUpdateState msg || _menu == null)
            return;

        _menu.RecipesByBlueprintType = msg.RecipeBitsetByBlueprintType;
        _menu.UpdateCategories();
        _menu.PopulateQueueList(msg.Queue);
        _menu.SetQueueInfo(msg.CurrentlyProducing);
        _menu.SetResearchServerConnected(msg.HasResearchServer);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _menu?.Window.Dispose();
    }
}
