// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Starmap.Components;
using Content.Shared._Lua.Starmap;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class SectorPayoutSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SectorOwnershipSystem _ownership = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<FactionPayoutCollectorComponent>(PayoutCollectorUiKey.Key, subs =>
        {
            subs.Event<PayoutCollectorClaimMessage>(OnClaim);
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var mapsOwners = _ownership.GetOwnerByMap();
        var q = AllEntityQuery<FactionPayoutCollectorComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var comp, out _))
        {
            var interval = TimeSpan.FromSeconds(Math.Max(1, comp.PayoutIntervalSeconds));
            if (comp.LastAccrualAt == TimeSpan.Zero)
            { comp.LastAccrualAt = now - interval; }
            var elapsed = now - comp.LastAccrualAt;
            if (elapsed < interval) continue;
            var ticks = (int)(elapsed / interval);
            var owned = mapsOwners.Count(kv => string.Equals(kv.Value, comp.Faction, StringComparison.Ordinal));
            if (owned > 0 && comp.PayoutPerSector > 0 && ticks > 0)
            {
                var add = ticks * owned * comp.PayoutPerSector;
                comp.Accumulated += add;
            }
            comp.LastAccrualAt += interval * ticks;
        }
    }

    private void OnUiOpened(Entity<FactionPayoutCollectorComponent> ent, ref BoundUIOpenedEvent args)
    {
        var owned = _ownership.GetOwnerByMap().Count(kv => string.Equals(kv.Value, ent.Comp.Faction, StringComparison.Ordinal));
        var state = new PayoutCollectorBuiState(owned, ent.Comp.Accumulated, ent.Comp.Faction);
        _ui.SetUiState(ent.Owner, PayoutCollectorUiKey.Key, state);
    }

    private void OnClaim(Entity<FactionPayoutCollectorComponent> ent, ref PayoutCollectorClaimMessage msg)
    {
        var amount = ent.Comp.Accumulated;
        if (amount <= 0 || string.IsNullOrWhiteSpace(ent.Comp.CurrencyPrototypePerUnit))
        {
            var state = new PayoutCollectorBuiState(_ownership.GetOwnerByMap().Count(kv => string.Equals(kv.Value, ent.Comp.Faction, StringComparison.Ordinal)), ent.Comp.Accumulated, ent.Comp.Faction);
            _ui.SetUiState(ent.Owner, PayoutCollectorUiKey.Key, state); return;
        }
        ent.Comp.Accumulated = 0;
        var xform = Transform(ent.Owner);
        for (var i = 0; i < amount; i++)
        { EntityManager.SpawnEntity(ent.Comp.CurrencyPrototypePerUnit, xform.Coordinates); }
        var state2 = new PayoutCollectorBuiState(_ownership.GetOwnerByMap().Count(kv => string.Equals(kv.Value, ent.Comp.Faction, StringComparison.Ordinal)), ent.Comp.Accumulated, ent.Comp.Faction);
        _ui.SetUiState(ent.Owner, PayoutCollectorUiKey.Key, state2);
    }
}


