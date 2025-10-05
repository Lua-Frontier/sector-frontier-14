// SPDX-FileCopyrightText: 2025 Ilya246
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Mono.GridClaimer;

/// <summary>
/// Causes this entity to be able to be used to prevent this grid from despawning.
/// </summary>
[RegisterComponent]
public sealed partial class GridClaimerComponent : Component
{
    /// <summary>
    /// Whether to require this entity to be anchored.
    /// </summary>
    [DataField]
    public bool RequireAnchored = true;

    /// <summary>
    /// The grid we're currently claiming.
    /// </summary>
    [DataField]
    public EntityUid? ClaimingGrid = null;

    /// <summary>
    /// Whether the grid we're claiming had OwnedDebrisComponent.
    /// </summary>
    [DataField]
    public bool WasDebris = false;
}
