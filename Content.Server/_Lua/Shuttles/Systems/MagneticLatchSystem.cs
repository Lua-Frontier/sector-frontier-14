// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Collections.Generic;
using Content.Server._Lua.Shuttles.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Lua.Shuttles;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Shuttles.Systems;

public sealed class MagneticLatchSystem : EntitySystem
{
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MagneticLatchComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<MagneticLatchComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MagneticLatchComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<MagneticGrabberComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<MagneticLatchTargetComponent, EntityTerminatingEvent>(OnLatchedTargetTerminating);
    }

    private void OnGetVerbs(EntityUid uid, MagneticLatchComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess) return;
        if (component.JointId == null || component.OwnerGrid == null) return;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("magnetic-latch-shutdown-verb"),
            Priority = 1,
            Act = () =>
            { Shutdown(uid, component, args.User); }
        });
    }

    private void OnShutdown(EntityUid uid, MagneticLatchComponent component, ComponentShutdown args)
    { Shutdown(uid, component, null); }

    private void OnAnchorChanged(EntityUid uid, MagneticLatchComponent component, ref AnchorStateChangedEvent args)
    { if (!args.Anchored) Shutdown(uid, component, null); }

    private void Shutdown(EntityUid uid, MagneticLatchComponent component, EntityUid? user, bool force = false)
    {
        if (component.JointId == null || component.OwnerGrid == null) return;
        if (!force && (IsInFtl(component.OwnerGrid.Value) || (component.TargetGrid != null && IsInFtl(component.TargetGrid.Value))))
        {
            if (user != null) _popup.PopupEntity(Loc.GetString("magnetic-latch-shutdown-blocked-ftl"), uid, user.Value);
            return;
        }
        _joints.RemoveJoint(component.OwnerGrid.Value, component.JointId);
        if (component.LatchedToEntity != null &&
            TryComp(component.LatchedToEntity.Value, out MagneticLatchTargetComponent? target))
        {
            target.Magnets.Remove(uid);
            if (target.Magnets.Count == 0)
                RemComp<MagneticLatchTargetComponent>(component.LatchedToEntity.Value);
        }
        if (TryComp(uid, out DockingComponent? dock))
        {
            dock.DockedWith = null;

        }
        _appearance.SetData(uid, MagneticLatchVisuals.State, MagneticLatchVisualState.Idle);
        SetCooldown(uid);
        component.JointId = null;
        component.OwnerGrid = null;
        component.TargetGrid = null;
        component.LatchedToEntity = null;

        if (user != null) _popup.PopupEntity(Loc.GetString("magnetic-latch-shutdown"), uid, user.Value);
    }

    public void ShutdownLatch(EntityUid magnet)
    {
        if (!TryComp(magnet, out MagneticLatchComponent? latch)) return;
        Shutdown(magnet, latch, null);
    }

    private void OnSignalReceived(EntityUid uid, MagneticGrabberComponent component, ref SignalReceivedEvent args)
    {
        if (args.Port != "ShutdownMagnet") return;
        ShutdownLatch(uid);
    }

    private void OnLatchedTargetTerminating(EntityUid uid, MagneticLatchTargetComponent component, ref EntityTerminatingEvent args)
    {
        var magnets = new List<EntityUid>(component.Magnets);
        foreach (var magnet in magnets)
        {
            if (TerminatingOrDeleted(magnet)) continue;
            if (!TryComp(magnet, out MagneticLatchComponent? latch)) continue;
            Shutdown(magnet, latch, user: null, force: true);
        }
    }

    private bool IsInFtl(EntityUid grid)
    {
        if (!TryComp(grid, out FTLComponent? ftl)) return false;
        return ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving;
    }

    private void SetCooldown(EntityUid magnet)
    {
        var cd = EnsureComp<MagneticGrabberCooldownComponent>(magnet);
        cd.NextLatchAllowed = _timing.CurTime + TimeSpan.FromSeconds(10);
    }
}

