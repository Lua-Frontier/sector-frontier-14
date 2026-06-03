/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Shuttles;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class UnifiedDriveComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public float InterstellarCooldownSeconds = 600f;

    [ViewVariables]
    public TimeSpan InterstellarCooldownEndsAt = TimeSpan.Zero;

    [DataField]
    [AutoNetworkedField]
    public float LocalFTLRange = 512f;

    [DataField]
    [AutoNetworkedField]
    public float LocalFTLCooldown = 10f;

    [DataField]
    [AutoNetworkedField]
    public float LocalFTLHyperSpaceTime = 20f;

    [DataField]
    [AutoNetworkedField]
    public float LocalFTLStartupTime = 5.5f;

    [DataField]
    [AutoNetworkedField]
    public bool MassAffectedDrive = true;

    [DataField]
    [AutoNetworkedField]
    public float DriveMassMultiplier = 1f;

    [DataField]
    [AutoNetworkedField]
    public float ThermalSignature = 2000000f;

    [DataField]
    [AutoNetworkedField]
    public bool SkipHyperspace = false;

    [DataField]
    [AutoNetworkedField]
    public float SkipHyperspaceEmpRange = 60f;
}
