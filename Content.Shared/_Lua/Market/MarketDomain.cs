// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Shared._NF.Market
{
    /// <summary>
    /// Distinct dynamic pricing domains. Each domain maintains an isolated dynamic state.
    /// Disabled means dynamic pricing is off (tax only).
    /// </summary>
    public enum MarketDomain
    {
        Default = 0,
        BlackMarket = 1,
        Syndicate = 2,
        Disabled = 3
    }
}


