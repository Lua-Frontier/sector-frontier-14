// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Lua.Common.ChatFilter;

public interface IChatFilterManager
{
    void Initialize();
    string FilterMessage(string message);
    bool IsProhibitedContent(EntityUid source, string message);
    bool IsProhibitedContent(ICommonSession source, string message);
}
