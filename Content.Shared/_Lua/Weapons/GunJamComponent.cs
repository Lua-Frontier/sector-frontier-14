// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GunJamComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxJamChance = 0.05f;

    [DataField, AutoNetworkedField]
    public float JamThreshold = 0.5f;

    [DataField, AutoNetworkedField]
    public float DamagePerShot = 5f;

    [DataField, AutoNetworkedField]
    public bool IsJammed = false;

    [DataField, AutoNetworkedField]
    public bool CanJam = true;

    [DataField, AutoNetworkedField]
    public bool IsEnergyWeapon = false;

    [DataField, AutoNetworkedField]
    public float EnergyJamDuration = 3f;

    [DataField, AutoNetworkedField]
    public TimeSpan EnergyJamUntil = TimeSpan.Zero;

    // Not networked — tracked locally on each side independently to avoid server overwriting client cooldown
    [DataField]
    public TimeSpan NextPopupAllowed = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public float DestroyThreshold = 3000f;
}
