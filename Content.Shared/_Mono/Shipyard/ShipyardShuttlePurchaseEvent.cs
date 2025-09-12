// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Mono.Shipyard;

public sealed class ShipyardShuttlePurchaseEvent(EntityUid shuttle, EntityUid purchaser)
{
    public EntityUid Shuttle { get; } = shuttle;
    public EntityUid Purchaser { get; } = purchaser;
}
