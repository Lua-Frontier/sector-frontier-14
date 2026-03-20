// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Weapons;

[RegisterComponent, NetworkedComponent]
public sealed partial class MeleeDurabilityComponent : Component
{
    [DataField]
    public float DamagePerHit = 1f;

    [DataField]
    public float DamageChance = 0.25f;

    [DataField]
    public float DestroyThreshold = 100f;
}

[Serializable, NetSerializable]
public sealed class MeleeDurabilityComponentState : ComponentState
{
    public float DestroyThreshold;
    public MeleeDurabilityComponentState(float destroyThreshold)
    {
        DestroyThreshold = destroyThreshold;
    }
}
