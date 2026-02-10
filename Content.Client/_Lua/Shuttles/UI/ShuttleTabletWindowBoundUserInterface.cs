// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Client.UserInterface;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared._Lua.Shuttles.UI;

namespace Content.Client._Lua.Shuttles.UI;

public sealed partial class ShuttleTabletWindowBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ShuttleTabletWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ShuttleTabletWindow>();

        _window.OnInertiaDampeningModeChanged += OnInertiaDampeningModeChanged;
        _window.OnServiceFlagsChanged += OnServiceFlagsChanged;
        _window.OnSetTargetCoordinates += OnSetTargetCoordinates;
        _window.OnSetHideTarget += OnSetHideTarget;

        _window.DockRequest += OnDockRequest;
        _window.UndockRequest += OnUndockRequest;
        _window.UndockAllRequest += OnUndockAllRequest;
        _window.ToggleFTLLockRequest += OnToggleFTLLockRequest;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not ShuttleTabletWindowInterfaceState cState)
        {
            return;
        }

        _window.UpdateState(Owner, cState);
    }

    private void OnToggleFTLLockRequest(List<NetEntity> dockEntities, bool enabled)
    {
        SendMessage(new ToggleFTLLockRequestMessage(dockEntities, enabled));
    }

    private void OnUndockAllRequest(List<NetEntity> dockEntities)
    {
        SendMessage(new UndockAllRequestMessage(dockEntities));
    }

    private void OnUndockRequest(NetEntity entity)
    {
        SendMessage(new UndockRequestMessage()
        {
            DockEntity = entity,
        });
    }

    private void OnDockRequest(NetEntity entity, NetEntity target)
    {
        SendMessage(new DockRequestMessage()
        {
            DockEntity = entity,
            TargetDockEntity = target,
        });
    }

    private void OnInertiaDampeningModeChanged(NetEntity? entityUid, InertiaDampeningMode mode)
    {
        SendMessage(new SetInertiaDampeningRequest
        {
            ShuttleEntityUid = entityUid,
            Mode = mode,
        });
    }

    private void OnServiceFlagsChanged(NetEntity? entityUid, ServiceFlags flags)
    {
        SendMessage(new SetServiceFlagsRequest
        {
            ShuttleEntityUid = entityUid,
            ServiceFlags = flags,
        });
    }

    private void OnSetTargetCoordinates(NetEntity? entityUid, Vector2 position)
    {
        SendMessage(new SetTargetCoordinatesRequest
        {
            ShuttleEntityUid = entityUid,
            TrackedPosition = position,
            TrackedEntity = NetEntity.Invalid
        });
    }

    private void OnSetHideTarget(NetEntity? entityUid, bool hide)
    {
        SendMessage(new SetHideTargetRequest
        {
            Hidden = hide
        });
    }
}
