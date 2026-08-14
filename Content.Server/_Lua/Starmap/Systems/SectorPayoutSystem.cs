// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server._Lua.Company;
using Content.Server._Lua.Starmap.Components;
using Content.Shared._Lua.Starmap;
using Content.Shared.IdentityManagement;
using Content.Shared.Lua.CLVar;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server._Lua.Starmap.Systems;

public sealed class SectorPayoutSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly FactionOwnedStationSystem _ownedStations = default!;

    private int _intervalSeconds = 3600;
    private int _perStation = 1;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CLVars.StationPayoutIntervalSeconds, v => _intervalSeconds = Math.Max(1, v), true);
        Subs.CVar(_cfg, CLVars.StationPayoutPerStation, v => _perStation = Math.Max(0, v), true);

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
        var interval = TimeSpan.FromSeconds(Math.Max(1, _intervalSeconds));
        var perStation = _perStation;
        var q = AllEntityQuery<FactionPayoutCollectorComponent, TransformComponent>();
        while (q.MoveNext(out var uid, out var comp, out _))
        {
            if (comp.LastAccrualAt == TimeSpan.Zero)
                comp.LastAccrualAt = now - interval;

            var elapsed = now - comp.LastAccrualAt;
            if (elapsed < interval)
                continue;

            var ticks = (int) (elapsed / interval);
            var owned = _ownedStations.CountOwnedStations(comp.Faction);
            var accrued = false;
            if (owned > 0 && perStation > 0 && ticks > 0)
            {
                comp.Accumulated += ticks * owned * perStation;
                accrued = true;
            }

            comp.LastAccrualAt += interval * ticks;
            if (accrued || ticks > 0)
                PushUi((uid, comp));
        }
    }

    private void OnUiOpened(Entity<FactionPayoutCollectorComponent> ent, ref BoundUIOpenedEvent args)
    {
        PushUi(ent);
    }

    private void OnClaim(Entity<FactionPayoutCollectorComponent> ent, ref PayoutCollectorClaimMessage msg)
    {
        var amount = ent.Comp.Accumulated;
        if (amount <= 0 || string.IsNullOrWhiteSpace(ent.Comp.CurrencyPrototypePerUnit))
        {
            PushUi(ent);
            return;
        }

        ent.Comp.Accumulated = 0;
        ent.Comp.ClaimHistory.Insert(0, new PayoutClaimHistoryEntry(Identity.Name(msg.Actor, EntityManager), amount));
        if (ent.Comp.ClaimHistory.Count > FactionPayoutCollectorComponent.MaxClaimHistory)
            ent.Comp.ClaimHistory.RemoveRange(
                FactionPayoutCollectorComponent.MaxClaimHistory,
                ent.Comp.ClaimHistory.Count - FactionPayoutCollectorComponent.MaxClaimHistory);

        var xform = Transform(ent.Owner);
        for (var i = 0; i < amount; i++)
            EntityManager.SpawnEntity(ent.Comp.CurrencyPrototypePerUnit, xform.Coordinates);

        PushUi(ent);
    }

    private void PushUi(Entity<FactionPayoutCollectorComponent> ent)
    {
        var owned = _ownedStations.CountOwnedStations(ent.Comp.Faction);
        var interval = TimeSpan.FromSeconds(Math.Max(1, _intervalSeconds));
        var last = ent.Comp.LastAccrualAt == TimeSpan.Zero
            ? _timing.CurTime
            : ent.Comp.LastAccrualAt;
        var nextPayoutAt = last + interval;
        var history = ent.Comp.ClaimHistory
            .Select(e => new PayoutClaimHistoryEntry(e.CharacterName, e.Amount))
            .ToList();
        var state = new PayoutCollectorBuiState(
            owned,
            ent.Comp.Accumulated,
            ent.Comp.Faction,
            _perStation,
            _intervalSeconds,
            nextPayoutAt,
            history);
        _ui.SetUiState(ent.Owner, PayoutCollectorUiKey.Key, state);
    }
}
