// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Lua.Common.Info;

namespace Content.Client.Lua.Info;

public sealed class PublicOfferRulesGateSystem : EntitySystem, IPublicOfferRulesGate
{
    public bool ShouldDeferRulesDisplay { get; set; }
}
