// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using System.Reflection;
using Content.Server._Lua.Shuttles.Commands;
using JetBrains.Annotations;
using Robust.Shared.Console;
using Robust.Shared.IoC;

namespace Content.Server._Lua.Shuttles.Systems;

[UsedImplicitly]
public sealed class SaveGridCommandSystem : EntitySystem
{
    private const string CommandName = "savegrid";
    [Dependency] private readonly IConsoleHost _console = default!;
    [Dependency] private readonly IDynamicTypeFactory _factory = default!;

    public override void Initialize()
    {
        base.Initialize();
        ReplaceEngineSaveGrid();
        _console.RegisterCommand(_factory.CreateInstance<SaveGridCommand>());
    }

    public override void Shutdown()
    {
        if (_console.AvailableCommands.ContainsKey(CommandName))
            _console.UnregisterCommand(CommandName);
        base.Shutdown();
    }

    private void ReplaceEngineSaveGrid()
    {
        if (!_console.AvailableCommands.ContainsKey(CommandName))
            return;
        var autoRegistered = FindAutoRegisteredCommands(_console);
        autoRegistered?.Remove(CommandName);
        _console.UnregisterCommand(CommandName);
    }

    private static HashSet<string>? FindAutoRegisteredCommands(IConsoleHost console)
    {
        for (var type = console.GetType(); type != null; type = type.BaseType)
        {
            if (type.Name != "ConsoleHost")
                continue;
            var field = type.GetField("_autoRegisteredCommands", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(console) as HashSet<string>;
        }
        return null;
    }
}
