// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Threading.Tasks;
using Robust.Shared.Network;

namespace Content.Shared._Lua.Info;

public interface IPublicOfferGate
{
    Task<bool> HasAcceptedOfferAsync(NetUserId userId);
}
