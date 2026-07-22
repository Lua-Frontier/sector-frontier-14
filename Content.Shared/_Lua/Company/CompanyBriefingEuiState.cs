// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Eui;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Company;

[Serializable, NetSerializable]
public sealed class CompanyBriefingEuiState(string title, Color color, string text) : EuiStateBase
{
    public readonly string Title = title;
    public readonly Color Color = color;
    public readonly string Text = text;
}
