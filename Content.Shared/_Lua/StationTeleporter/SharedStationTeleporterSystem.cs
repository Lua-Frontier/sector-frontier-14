// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.StationTeleporter.Components;
using Content.Shared.Audio;
using Content.Shared.Power;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using System.Linq;

namespace Content.Shared._Lua.StationTeleporter;

public abstract class SharedStationTeleporterSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationTeleporterComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<StationTeleporterComponent, LinkedEntityChangedEvent>(OnLinkedChanged);
        SubscribeLocalEvent<StationTeleporterComponent, StationTeleporterClickMessage>(OnTeleporterClick);
        SubscribeLocalEvent<StationTeleporterComponent, StationTeleporterRenameMessage>(OnTeleporterRename);
    }

    private void OnPowerChanged(Entity<StationTeleporterComponent> ent, ref PowerChangedEvent args)
    {
        var (uid, comp) = ent;
        if (!args.Powered)
        {
            if (TryComp<LinkedEntityComponent>(uid, out var link) && link.LinkedEntities.Count > 0)
            {
                comp.LastLink = link.LinkedEntities.First();
                Unlink(uid, comp.LastLink.Value);
            }
        }
        else
        {
            if (comp.LastLink != null && Exists(comp.LastLink.Value))
            {
                var partner = comp.LastLink.Value;
                if (!TryComp<LinkedEntityComponent>(partner, out var partnerLink) || partnerLink.LinkedEntities.Count == 0) _link.TryLink(uid, partner);
            }
        }
    }

    private void OnLinkedChanged(EntityUid uid, StationTeleporterComponent comp, LinkedEntityChangedEvent args)
    {
        var hasLink = args.NewLinks.Count > 0;
        if (TryComp<AmbientSoundComponent>(uid, out var ambient)) _ambientSound.SetAmbience(uid, hasLink, ambient);
        if (hasLink) _audio.PlayPredicted(comp.LinkSound, uid, null);
        else _audio.PlayPredicted(comp.UnlinkSound, uid, null);
        _appearance.SetData(uid, TeleporterPortalVisuals.Color, hasLink ? Color.Cyan : Color.Gray);
        if (!_net.IsServer) return;
    }

    private void OnTeleporterClick(EntityUid uid, StationTeleporterComponent comp, StationTeleporterClickMessage args)
    {
        if (!_net.IsServer) return;
        var target = GetEntity(args.TargetUid);
        if (!Exists(target) || target == uid) return;
        if (!TryComp<StationTeleporterComponent>(target, out var targetTp) || targetTp.Type != comp.Type) return;
        var selfIsLinked = TryComp<LinkedEntityComponent>(uid, out var selfLink) && selfLink.LinkedEntities.Count > 0;
        var targetIsLinked = TryComp<LinkedEntityComponent>(target, out var targetLink) && targetLink.LinkedEntities.Count > 0;
        if (selfIsLinked && selfLink!.LinkedEntities.First() == target)
        {
            Unlink(uid, target);
            return;
        }
        if (targetIsLinked) Unlink(target, targetLink!.LinkedEntities.First());
        if (selfIsLinked) Unlink(uid, selfLink!.LinkedEntities.First());
        _link.TryLink(uid, target);
        comp.LastLink = target;
        targetTp.LastLink = uid;
        Dirty(uid, comp);
        Dirty(target, targetTp);
    }

    private void OnTeleporterRename(EntityUid uid, StationTeleporterComponent comp, StationTeleporterRenameMessage args)
    {
        if (!_net.IsServer) return;
        if (comp.Type != TeleporterType.Local) return;
        var newName = args.NewName.Trim();
        if (newName.Length > 64) newName = newName[..64];
        comp.CustomName = string.IsNullOrEmpty(newName) ? null : newName;
        Dirty(uid, comp);
    }

    private void Unlink(EntityUid a, EntityUid b)
    {
        _link.TryUnlink(a, b);
        if (TryComp<StationTeleporterComponent>(a, out var tpA)) tpA.LastLink = null;
        if (TryComp<StationTeleporterComponent>(b, out var tpB)) tpB.LastLink = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer) return;
        var uiQuery = EntityQueryEnumerator<StationTeleporterComponent>();
        while (uiQuery.MoveNext(out var uid, out var comp))
        {
            if (!IsUiOpen(uid)) continue;
            comp.UpdateTimer += frameTime;
            if (comp.UpdateTimer < comp.UpdateFrequency) continue;
            comp.UpdateTimer = 0f;
            UpdateUserInterface(uid, comp);
        }
    }

    protected virtual bool IsUiOpen(EntityUid uid) => false;
    protected virtual void UpdateUserInterface(EntityUid uid, StationTeleporterComponent comp) { }
}
