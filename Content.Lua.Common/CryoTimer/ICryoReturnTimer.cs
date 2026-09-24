// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameObjects;

namespace Content.Lua.Common.CryoTimer;

public interface ICryoReturnTimer : IEntitySystem
{
    TimeSpan? CryoReturnTime { get; }
    event Action? CryoReturnReseted;
}
