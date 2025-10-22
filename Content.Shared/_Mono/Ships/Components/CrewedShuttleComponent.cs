// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Mono.Ships.Components;

/// <summary>
/// This is used for knowing whether a shuttle should be forced to be crewed.
/// </summary>
[RegisterComponent]
public sealed partial class CrewedShuttleComponent : Component
{
    [DataField]
    public List<EntityUid> ShuttleConsoles;

    [DataField]
    public List<EntityUid> GunneryConsoles;
}
