// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using System.Linq;
using Content.Server._Lua.Starmap.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class StarmapListStarsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    public string Command => "starmap_list";
    public string Description => Loc.GetString("cmd-starmap-list-desc");
    public string Help => Loc.GetString("cmd-starmap-list-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var starmap = _entManager.System<StarmapSystem>();
        var stars = starmap.CollectStarsFresh(updateCache: true);
        if (stars.Count == 0)
        { shell.WriteLine(Loc.GetString("cmd-starmap-list-no-stars")); return; }
        for (var i = 0; i < stars.Count; i++)
        {
            var star = stars[i];
            shell.WriteLine(Loc.GetString("cmd-starmap-list-line", ("index", i), ("mapId", (int) star.Map), ("name", star.Name), ("position", star.Position)));
        }
    }
    public CompletionResult GetCompletion(IConsoleShell shell, string[] args) => CompletionResult.Empty;
}

[AdminCommand(AdminFlags.Admin)]
public sealed class RegenHyperlineCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    public string Command => "regenhyperline";
    public string Description => Loc.GetString("cmd-regenhyperline-desc");
    public string Help => Loc.GetString("cmd-regenhyperline-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var starmap = _entManager.System<StarmapSystem>();
        starmap.ClearHyperlaneOverrides(invalidateCache: false);
        try
        {
            var sectorStarMap = _entManager.System<SectorStarMapSystem>();
            sectorStarMap.ForceUpdateAllStarMaps();
        }
        catch (Exception)
        { }
        starmap.CollectStarsFresh(updateCache: true);
        starmap.RefreshConsoles();
        shell.WriteLine(Loc.GetString("cmd-regenhyperline-done"));
    }
    public CompletionResult GetCompletion(IConsoleShell shell, string[] args) => CompletionResult.Empty;
}

[AdminCommand(AdminFlags.Admin)]
public sealed class HypelineCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    public string Command => "hyperline";
    public string Description => Loc.GetString("cmd-hypeline-desc");
    public string Help => Loc.GetString("cmd-hypeline-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        { shell.WriteError(Loc.GetString("shell-wrong-arguments-number")); return; }
        if (!int.TryParse(args[0], out var aVal) || !int.TryParse(args[1], out var bVal))
        { shell.WriteError(Loc.GetString("shell-invalid-map-id")); return; }
        var mapA = new MapId(aVal);
        var mapB = new MapId(bVal);
        var starmap = _entManager.System<StarmapSystem>();
        var stars = starmap.CollectStars();
        var hasA = stars.Any(s => s.Map == mapA);
        var hasB = stars.Any(s => s.Map == mapB);
        if (!hasA || !hasB)
        { shell.WriteError(Loc.GetString("cmd-hypeline-star-not-found", ("mapA", (int) mapA), ("mapB", (int) mapB))); return; }
        var added = starmap.TryAddHyperlane(mapA, mapB);
        if (added)
        { shell.WriteLine(Loc.GetString("cmd-hypeline-added", ("mapA", (int) mapA), ("mapB", (int) mapB))); }
        else
        { shell.WriteLine(Loc.GetString("cmd-hypeline-exists", ("mapA", (int) mapA), ("mapB", (int) mapB))); }
    }
    public CompletionResult GetCompletion(IConsoleShell shell, string[] args) => CompletionResult.Empty;
}

[AdminCommand(AdminFlags.Admin)]
public sealed class UnhyperlineCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    public string Command => "unhyperline";
    public string Description => Loc.GetString("cmd-unhyperline-desc");
    public string Help => Loc.GetString("cmd-unhyperline-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        { shell.WriteError(Loc.GetString("shell-wrong-arguments-number")); return; }
        if (!int.TryParse(args[0], out var aVal) || !int.TryParse(args[1], out var bVal))
        { shell.WriteError(Loc.GetString("shell-invalid-map-id")); return; }
        var mapA = new MapId(aVal);
        var mapB = new MapId(bVal);
        var starmap = _entManager.System<StarmapSystem>();
        var stars = starmap.CollectStars();
        var hasA = stars.Any(s => s.Map == mapA);
        var hasB = stars.Any(s => s.Map == mapB);
        if (!hasA || !hasB)
        { shell.WriteError(Loc.GetString("cmd-hypeline-star-not-found", ("mapA", (int)mapA), ("mapB", (int)mapB))); return; }
        var existed = starmap.TryBlockHyperlane(mapA, mapB);
        if (existed)
        { shell.WriteLine(Loc.GetString("cmd-unhyperline-removed", ("mapA", (int)mapA), ("mapB", (int)mapB))); }
        else { shell.WriteLine(Loc.GetString("cmd-unhyperline-blocked-only", ("mapA", (int)mapA), ("mapB", (int)mapB))); }
    }
    public CompletionResult GetCompletion(IConsoleShell shell, string[] args) => CompletionResult.Empty;
}


