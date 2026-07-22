// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.RoundEnd;

[Serializable, NetSerializable]
public sealed class EvacuationTimerUpdateEvent : EntityEventArgs
{
    public TimeSpan? ExpectedEvacuationTime { get; init; }
}
