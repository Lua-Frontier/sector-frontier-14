// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.SelfDestruct;

[Serializable, NetSerializable]
public sealed class SelfDestructEnterDigitMessage : BoundUserInterfaceMessage
{
    public int Digit;
    public SelfDestructEnterDigitMessage(int digit) { Digit = digit; }
}

[Serializable, NetSerializable]
public sealed class SelfDestructClearMessage : BoundUserInterfaceMessage {}

[Serializable, NetSerializable]
public sealed class SelfDestructConfirmWarningMessage : BoundUserInterfaceMessage {}

[Serializable, NetSerializable]
public sealed class SelfDestructSavePinMessage : BoundUserInterfaceMessage {}

[Serializable, NetSerializable]
public sealed class SelfDestructArmMessage : BoundUserInterfaceMessage {}


