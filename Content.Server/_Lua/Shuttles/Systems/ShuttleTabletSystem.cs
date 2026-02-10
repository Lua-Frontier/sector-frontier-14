// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Audio;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared._NF.Shipyard.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Popups;
using Content.Server._Lua.Shuttles.Components;

namespace Content.Server._Lua.Shuttles.Systems;

public sealed class ShuttleTabletSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private const string IDContainerSlot = "id_container";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleTabletComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ShuttleTabletComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ShuttleTabletComponent, GridUidChangedEvent>(OnGridChanged);
        SubscribeLocalEvent<ShuttleTabletComponent, EntInsertedIntoContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<ShuttleTabletComponent, EntRemovedFromContainerMessage>(OnItemSlotChanged);
        SubscribeLocalEvent<ShuttleTabletComponent, AfterInteractEvent>(OnConsoleLink);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var tabletQuery = EntityQueryEnumerator<ShuttleTabletComponent>();

        while (tabletQuery.MoveNext(out var tablet, out var tabletComp))
        {
            if (tabletComp.GridChangeRequired)
            {
                tabletComp.GridChangeRequired = false;
                RefreshTabletGrid(tablet);
            }
        }
    }

    private void OnStartup(EntityUid tablet, ShuttleTabletComponent tabletComp, ComponentStartup args)
    {
        _metaData.AddFlag(tablet, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnShutdown(EntityUid tablet, ShuttleTabletComponent tabletComp, ComponentShutdown args)
    {
        _metaData.RemoveFlag(tablet, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnGridChanged(EntityUid tablet, ShuttleTabletComponent component, GridUidChangedEvent args)
    {
        var shuttleUid = GetShuttleUid(tablet);

        if (shuttleUid == null || shuttleUid == EntityUid.Invalid || args.NewGrid == shuttleUid)
        {
            return;
        }

        component.GridChangeRequired = true;
    }

    private void OnConsoleLink(EntityUid tablet, ShuttleTabletComponent tabletComp, AfterInteractEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        var console = args.Target;

        if (!args.CanReach || !Exists(console))
        {
            return;
        }

        if (!TryComp<ShuttleConsoleComponent>(console, out _))
        {
            return;
        }

        var newConsole = tabletComp.LinkedConsole != console;
        var linkedString = newConsole ? "shuttle-tablet-console-linked" : "shuttle-tablet-console-unlinked";

        _audio.PlayPvs(tabletComp.LinkSound, tablet);
        _popup.PopupEntity(Loc.GetString(linkedString), tablet);
        tabletComp.LinkedConsole = newConsole ? console : null;

        args.Handled = true;
    }

    private void OnItemSlotChanged(EntityUid tablet, ShuttleTabletComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != IDContainerSlot)
        {
            return;
        }

        RefreshTabletGrid(tablet);
    }

    private void RefreshTabletGrid(EntityUid tablet)
    {
        var shuttleUid = GetShuttleUid(tablet);

        if (shuttleUid == null)
        {
            return;
        }

        RefreshTabletGrid(tablet, shuttleUid);
    }

    private void RefreshTabletGrid(EntityUid tablet, EntityUid? shuttleUid)
    {
        _transform.SetGridId(tablet, Transform(tablet), shuttleUid);
    }

    private EntityUid? GetShuttleUid(EntityUid tablet)
    {
        var card = _slots.GetItemOrNull(tablet, IDContainerSlot);

        if (card == null)
        {
            return null;
        }

        if (!TryComp<ShuttleDeedComponent>(card, out var deedComp))
        {
            return null;
        }

        return deedComp.ShuttleUid;
    }
}
