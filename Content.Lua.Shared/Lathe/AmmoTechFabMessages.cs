// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Lua.Shared.Lathe;

[Serializable, NetSerializable]
public sealed class AmmoTechFabEjectMessage : BoundUserInterfaceMessage
{
    public NetEntity Magazine;

    public AmmoTechFabEjectMessage(NetEntity magazine)
    {
        Magazine = magazine;
    }
}

[Serializable, NetSerializable]
public sealed class AmmoTechFabEjectAllMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AmmoTechFabSetCartridgeMessage : BoundUserInterfaceMessage
{
    public NetEntity Magazine;
    public string Cartridge = string.Empty;

    public AmmoTechFabSetCartridgeMessage(NetEntity magazine, string cartridge)
    {
        Magazine = magazine;
        Cartridge = cartridge;
    }
}
