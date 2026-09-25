// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Lua.Shared.Lathe;
using Content.Shared._NF.Lathe;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Lua.Lathe.UI;

[UsedImplicitly]
public sealed class AmmoTechFabBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AmmoTechFabMenu? _menu;

    public AmmoTechFabBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindowCenteredRight<AmmoTechFabMenu>();
        _menu.SetEntity(Owner);

        _menu.OnServerListButtonPressed += _ =>
        {
            SendMessage(new ConsoleServerSelectionMessage());
        };

        _menu.RecipeQueueAction += (recipe, amount) =>
        {
            SendMessage(new LatheQueueRecipeMessage(recipe, amount));
        };

        _menu.QueueDeleteAction += index => SendMessage(new LatheDeleteRequestMessage(index));
        _menu.QueueMoveUpAction += index => SendMessage(new LatheMoveRequestMessage(index, -1));
        _menu.QueueMoveDownAction += index => SendMessage(new LatheMoveRequestMessage(index, 1));
        _menu.DeleteFabricatingAction += () => SendMessage(new LatheAbortFabricationMessage());

        _menu.OnLoopCheckboxPressed += loop => SendMessage(new LatheSetLoopingMessage(loop));
        _menu.OnSkipCheckboxPressed += skip => SendMessage(new LatheSetSkipMessage(skip));
        _menu.AutoloaderEjectAction += magazine => SendMessage(new AmmoTechFabEjectMessage(magazine));
        _menu.AutoloaderEjectAllAction += () => SendMessage(new AmmoTechFabEjectAllMessage());
        _menu.AutoloaderSelectAction += (magazine, cartridge) =>
            SendMessage(new AmmoTechFabSetCartridgeMessage(magazine, cartridge));
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
}
