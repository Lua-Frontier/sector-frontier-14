// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.Lua.UIKit.Reputation;

public interface IReputationExamine : IEntitySystem
{
    void AddReputationButtons(EntityUid player, EntityUid target, BoxContainer clickExamineBox);
}
