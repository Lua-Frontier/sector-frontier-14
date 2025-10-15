// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.SelfDestruct;

[Serializable, NetSerializable]
public enum SelfDestructUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum SelfDestructStatus : byte
{
    Locked,
    Warning,
    SetupPin,
    AwaitPin,
    ReadyToArm,
    CountingDown
}

[Serializable, NetSerializable]
public enum SelfDestructVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum SelfDestructVisualState : byte
{
    Idle,
    Armed
}

[Serializable, NetSerializable]
public sealed class SelfDestructUiState : BoundUserInterfaceState
{
    public SelfDestructStatus Status;
    public int EnteredLength;
    public int MaxLength;
    public bool AllowArm;
    public int RemainingTime;
    public bool PinSet;
    public string EnteredText = string.Empty;
}


