// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Lua.Shared.GalBank.Events;

[Serializable, NetSerializable]
public sealed class GalBankTransferRequestMessage : CartridgeMessageEvent
{
    public string TargetCard = string.Empty;
    public int Amount;
}
