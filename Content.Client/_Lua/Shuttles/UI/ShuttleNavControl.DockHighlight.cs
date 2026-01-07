// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Shuttles.BUIStates;
namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    public NetEntity? HighlightDockPort { get; set; }
    partial void GetDockColorOverride(ref Color color, DockingPortState state)
    { if (HighlightDockPort.HasValue && state.Entity == HighlightDockPort.Value) { color = Color.Gold; } }
}


