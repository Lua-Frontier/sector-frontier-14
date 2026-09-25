// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Timing;

namespace Content.Lua.Server.Bank;

public sealed partial class BankSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
}
