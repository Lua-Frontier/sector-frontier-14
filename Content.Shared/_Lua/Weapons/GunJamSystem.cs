// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Lua.Weapons;

public abstract class SharedGunJamSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunJamComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<GunJamComponent, BoltClosedEvent>(OnBoltClosed);
    }

    private void OnShotAttempted(Entity<GunJamComponent> ent, ref ShotAttemptedEvent args)
    {
        var now = _timing.CurTime;
        if (ent.Comp.IsJammed)
        {
            args.Cancel();
            ShowPopupOnce(ent, args.User, "gun-jam-blocked", ref ent.Comp.NextPopupAllowed, now);
            return;
        }
        if (ent.Comp.IsEnergyWeapon && ent.Comp.EnergyJamUntil > now)
        {
            args.Cancel();
            ShowPopupOnce(ent, args.User, "gun-jam-energy-blocked", ref ent.Comp.NextPopupAllowed, now);
        }
    }

    private void ShowPopupOnce(Entity<GunJamComponent> ent, EntityUid user, string locKey,
        ref TimeSpan nextAllowed, TimeSpan now)
    {
        if (now < nextAllowed)
            return;

        nextAllowed = now + TimeSpan.FromSeconds(1.5);

        if (_net.IsClient)
            _popup.PopupEntity(Loc.GetString(locKey), ent.Owner, user, PopupType.SmallCaution);
    }

    private void OnBoltClosed(Entity<GunJamComponent> ent, ref BoltClosedEvent _)
    {
        if (!ent.Comp.IsJammed)
            return;

        ent.Comp.IsJammed = false;
        Dirty(ent);
    }
}
