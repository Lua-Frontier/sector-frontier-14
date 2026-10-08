// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Client.Items;
using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Ame.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lua.Ame;

public sealed class AmeFuelContainerHudSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<AmeFuelContainerComponent>(ent => new AmeFuelContainerStatusControl(ent));
    }
}

public sealed class AmeFuelContainerStatusControl : PollingItemStatusControl<AmeFuelContainerStatusControl.Data>
{
    private readonly AmeFuelContainerComponent _container;
    private readonly RichTextLabel _label;

    public AmeFuelContainerStatusControl(Entity<AmeFuelContainerComponent> container)
    {
        _container = container.Comp;
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
    }

    protected override Data PollData()
    {
        return new Data(_container.FuelAmount, _container.FuelCapacity);
    }

    protected override void Update(in Data data)
    {
        var low = data.Amount * 4 < data.Capacity;
        _label.SetMarkup(Loc.GetString("ame-fuel-container-component-on-examine-detailed-message",
            ("colorName", low ? "darkorange" : "orange"),
            ("amount", data.Amount),
            ("capacity", data.Capacity)));
    }

    public readonly record struct Data(int Amount, int Capacity);
}
