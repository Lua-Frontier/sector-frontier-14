// SPDX-FileCopyrightText: 2024 Aviu00
// SPDX-FileCopyrightText: 2025 Redrover1760
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._White.Blocking;

[RegisterComponent]
public sealed partial class RechargeableBlockingComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DischargedRechargeRate = 4f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ChargedRechargeRate = 5f;

    [ViewVariables]
    public bool Discharged;
}
