// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Starmap.Components;
using Content.Shared._Lua.Starmap;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class SectorCaptureSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SectorOwnershipSystem _ownership = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<SectorCaptureComponent>(SectorCaptureUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<StartSectorCaptureBuiMsg>(OnStartCapture);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var q = AllEntityQuery<SectorCaptureComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.IsCapturing) continue;
            if (!xform.Anchored || xform.MapID == MapId.Nullspace || !_ownership.IsOnBeaconGrid(xform.MapID, xform))
            {
                comp.IsCapturing = false;
                comp.CaptureStartedAt = TimeSpan.Zero;
                PushState(uid, comp);
                continue;
            }
            var duration = Math.Max(0.1f, comp.CaptureDurationSeconds);
            var elapsed = (now - comp.CaptureStartedAt).TotalSeconds;
            if (elapsed >= duration)
            {
                comp.IsCapturing = false;
                comp.CaptureStartedAt = TimeSpan.Zero;
                EnsureComp<StarMapSectorColorOverrideComponent>(uid, out var banner);
                banner.Faction = comp.Faction;
                banner.ColorHex = comp.ColorHex;
                Dirty(uid, banner);
                PushState(uid, comp);
            }
            else
            {
                PushState(uid, comp);
            }
        }
    }

    private void OnUiOpened(Entity<SectorCaptureComponent> ent, ref BoundUIOpenedEvent args)
    { PushState(ent.Owner, ent.Comp); }

    private void OnStartCapture(Entity<SectorCaptureComponent> ent, ref StartSectorCaptureBuiMsg args)
    {
        if (ent.Comp.IsCapturing) return;
        if (!TryComp<TransformComponent>(ent, out var xform)) return;
        if (!xform.Anchored || xform.MapID == MapId.Nullspace) return;
        if (!_ownership.IsOnBeaconGrid(xform.MapID, xform)) return;
        if (HasComp<StarMapSectorColorOverrideComponent>(ent)) return;
        ent.Comp.IsCapturing = true;
        ent.Comp.CaptureStartedAt = _timing.CurTime;
        PushState(ent.Owner, ent.Comp);
    }

    private void PushState(EntityUid uid, SectorCaptureComponent comp)
    {
        var now = _timing.CurTime;
        var progress = 0f;
        if (comp.IsCapturing && comp.CaptureDurationSeconds > 0)
        {
            progress = (float)((now - comp.CaptureStartedAt).TotalSeconds / comp.CaptureDurationSeconds);
            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;
        }
        var canCapture = false;
        try
        {
            if (TryComp<TransformComponent>(uid, out var xform) && xform.Anchored && xform.MapID != MapId.Nullspace)
            {
                if (_ownership.IsOnBeaconGrid(xform.MapID, xform) && !HasComp<StarMapSectorColorOverrideComponent>(uid))
                    canCapture = true;
            }
        }
        catch { }
        var state = new SectorCaptureBuiState(comp.IsCapturing, progress, comp.Faction, canCapture);
        _ui.SetUiState(uid, SectorCaptureUiKey.Key, state);
    }
}


