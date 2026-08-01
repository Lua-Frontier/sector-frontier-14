// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Server._Crescent.ShipShields.Components;
using Content.Shared._Crescent.ShipShields;
using Content.Shared._Lua.SpaceHazards;

namespace Content.Server._Crescent.ShipShields;

public sealed partial class ShipShieldsSystem
{
    private void InitializeNebulaAbsorption()
    {
        SubscribeLocalEvent<ShipShieldedComponent, NebulaShieldHitAttemptEvent>(OnNebulaShieldHit);
    }

    private void OnNebulaShieldHit(EntityUid gridUid, ShipShieldedComponent shielded, ref NebulaShieldHitAttemptEvent args)
    {
        if (args.Absorbed || args.Load <= 0f || shielded.Source is not { } source ||
            !TryComp(source, out ShipShieldEmitterComponent? emitter) || emitter.Recharging)
        {
            return;
        }

        emitter.Damage += args.Load;
        emitter.Accumulator = 0f;
        args.Absorbed = true;
    }
}
