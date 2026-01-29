// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Shuttles;

[Serializable, NetSerializable]
public enum MagneticLatchVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum MagneticLatchVisualState : byte
{
    Idle,
    Latched
}

[Serializable, NetSerializable]
public enum MagneticLatchVisualLayers : byte
{
    Base
}

