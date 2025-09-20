// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
using Robust.Shared.Console;

namespace Content.Client._Lua.Mapping;

public sealed class DrawLineClientCommand : IConsoleCommand
{
    public string Command => "drawline";
    public string Description => "Линейка и категории шаттлов";
    public string Help => "drawline";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sys = EntitySystem.Get<DrawLineSystem>();
        sys.SendDrawRequest();
    }
}


