// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Inventory;
using Robust.Shared.Timing;

namespace Content.Server._NF.BloodCult;

public sealed class BloodCultAmuletSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly RechargeBasicEntityAmmoSystem _recharge = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private const float GlobalCooldown = 440f;
    private readonly Dictionary<EntityUid, TimeSpan> _playerLastUse = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BasicEntityAmmoProviderComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(EntityUid uid, BasicEntityAmmoProviderComponent component, ref ShotAttemptedEvent args)
    {
        if (MetaData(uid).EntityPrototype?.ID != "ClothingNeckAmuletBloodCult") return;
        var owner = args.User;
        if (_playerLastUse.TryGetValue(owner, out var lastUse))
        {
            var timeSinceLastUse = _timing.CurTime - lastUse;
            if (timeSinceLastUse.TotalSeconds < GlobalCooldown) { args.Cancel(); return; }
        }
        _playerLastUse[owner] = _timing.CurTime;
        ResetAllAmuletsForPlayer(owner);
    }

    private void ResetAllAmuletsForPlayer(EntityUid player)
    {
        var query = EntityQueryEnumerator<BasicEntityAmmoProviderComponent, RechargeBasicEntityAmmoComponent>();

        while (query.MoveNext(out var amuletUid, out var ammoComp, out var rechargeComp))
        {
            if (MetaData(amuletUid).EntityPrototype?.ID != "ClothingNeckAmuletBloodCult") continue;
            if (!IsAmuletOwnedByPlayer(amuletUid, player)) continue;
            _recharge.Reset(amuletUid, rechargeComp);
        }
    }

    private bool IsAmuletOwnedByPlayer(EntityUid amulet, EntityUid player)
    { return _inventory.TryGetSlotEntity(player, "neck", out var neckEntity) && neckEntity == amulet; }
}
