// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 RikuTheKiller
// SPDX-FileCopyrightText: 2025 ark1368
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// Copyright Rane (elijahrane@gmail.com) 2025
// All rights reserved. Relicensed under AGPL with permission

namespace Content.Server._Mono.FireControl;

[RegisterComponent]
public sealed partial class FireControlServerComponent : Component
{
    [ViewVariables]
    public EntityUid? ConnectedGrid = null;

    [ViewVariables]
    public HashSet<EntityUid> Controlled = [];

    [ViewVariables]
    public HashSet<EntityUid> Consoles = [];

    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> Leases;

    [ViewVariables, DataField]
    public int ProcessingPower;

    [ViewVariables]
    public int UsedProcessingPower;

    [ViewVariables, DataField]
    public int MaxConsoles = 1;

    [ViewVariables, DataField]
    public bool EnforceMaxConsoles;

    //Lua start
    [DataField]
    public bool UseSalvos = true;
    [DataField]
    public float SalvoPeriodSeconds = 3f;
    [DataField]
    public float SalvoWindowSeconds = 0.5f;
    [DataField]
    public float SalvoJitterSeconds = 0.12f;
    //Lua end
}
