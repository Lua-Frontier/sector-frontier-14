// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Lua.Shared.Stargate.Components;
using Content.Lua.Common.CLVar;
using Robust.Shared.Configuration;

namespace Content.Lua.Server.Stargate.Systems;

public sealed class StargateAddressDiskSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
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

        if (!_cfg.GetCVar(CLVars.StargateEnabled))
            return;

        var address = _registry.GetRandomPoolAddress();
        if (address == null)
            return;

        comp.Addresses.Add(new List<byte>(address));
        Dirty(uid, comp);
    }
}
