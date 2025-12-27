/*
 * LuaWorld - This file is licensed under AGPLv3
 * Copyright (c) 2025 LuaWorld Contributors
 * See AGPLv3.txt for details.
 */
using Robust.Shared.Serialization;
using Content.Shared.CartridgeLoader;

namespace Content.Shared._Lua.Bank.Events;

[Serializable, NetSerializable]
public sealed class YupiTransferRequestMessage : CartridgeMessageEvent
{
    public string TargetCode = string.Empty;
    public int Amount;
}
