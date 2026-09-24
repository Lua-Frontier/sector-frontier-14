using Content.Client.UserInterface;
using Content.Lua.UIKit.Machines;
using Content.Shared._NF.Lathe; // Frontier
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Lathe.UI
{
    [UsedImplicitly]
    public sealed class LatheBoundUserInterface : FactoryBoundUserInterface
    {
        [ViewVariables]
        private ILunaLatheMenu? _menu;
        public LatheBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            if (!IoCManager.Instance!.TryResolveType<ILuaMachineUiFactory>(out var factory))
                return;

            _menu = factory.CreateLatheMenu();
            OpenWindowCenteredRight(_menu.Window);
            _menu.SetEntity(Owner);

            _menu.OnServerListButtonPressed += _ =>
            {
                SendMessage(new ConsoleServerSelectionMessage());
            };

            _menu.RecipeQueueAction += (recipe, amount) =>
            {
                SendMessage(new LatheQueueRecipeMessage(recipe, amount));
            };

            // Frontier: lathe queue manipulation messages
            _menu.QueueDeleteAction += index => SendMessage(new LatheDeleteRequestMessage(index));
            _menu.QueueMoveUpAction += index => SendMessage(new LatheMoveRequestMessage(index, -1));
            _menu.QueueMoveDownAction += index => SendMessage(new LatheMoveRequestMessage(index, 1));
            _menu.DeleteFabricatingAction += () => SendMessage(new LatheAbortFabricationMessage());
            // End Frontier

            // <Mono>
            _menu.OnLoopCheckboxPressed += loop => SendMessage(new LatheSetLoopingMessage(loop));
            _menu.OnSkipCheckboxPressed += skip => SendMessage(new LatheSetSkipMessage(skip));
            // </Mono>
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not LatheUpdateState msg || _menu == null)
                return;

            _menu.Recipes = msg.Recipes;
            _menu.Refresh();
            _menu.PopulateQueueList(msg.Queue);
            _menu.SetQueueInfo(msg.CurrentlyProducing, msg.ProductionStartedAt, msg.ProductionLength);
            _menu.SetLooping(msg.Looping);
            _menu.SetSkipping(msg.Skipping);
            _menu.SetResearchServerConnected(msg.HasResearchServer);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _menu?.Window.Dispose();
        }
    }
}
