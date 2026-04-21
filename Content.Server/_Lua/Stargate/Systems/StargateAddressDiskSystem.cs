// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Stargate.Components;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateAddressDiskSystem : EntitySystem
{
    [Dependency] private readonly StargateAddressRegistrySystem _registry = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StargateAddressDiskComponent, MapInitEvent>(OnDiskMapInit);
    }

    private void OnDiskMapInit(EntityUid uid, StargateAddressDiskComponent comp, MapInitEvent args)
    {
        if (comp.Addresses.Count > 0)
            return;

        var address = _registry.GetRandomPoolAddress();
        if (address == null)
            return;

        comp.Addresses.Add(new List<byte>(address));
        Dirty(uid, comp);
    }
}
