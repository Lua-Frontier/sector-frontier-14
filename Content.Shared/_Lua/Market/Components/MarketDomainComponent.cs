// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Robust.Shared.GameStates;

namespace Content.Shared._Lua.Market.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class MarketDomainComponent : Component
    {
        [DataField]
        public _NF.Market.MarketDomain Domain = _NF.Market.MarketDomain.Default;
    }
}


