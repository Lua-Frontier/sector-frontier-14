// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using Content.Server.Administration;
using Content.Server._Lua.Shuttles.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Localization;
using Robust.Shared.Map.Components;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;

namespace Content.Server._Lua.Shuttles.Commands;

[Reflect(false)]
[AdminCommand(AdminFlags.Mapping)]
public sealed class SaveGridCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IResourceManager _resource = default!;

    public override string Command => "savegrid";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Usage: savegrid <entityUid> <path> [type]");
            shell.WriteError("Types: shuttle, station, event, shuttleai (optional)");
            return;
        }
        if (args.Length > 3)
        {
            shell.WriteError("Too many arguments.");
            return;
        }
        if (!NetEntity.TryParse(args[0], out var uidNet))
        {
            shell.WriteError("Not a valid entity ID.");
            return;
        }
        var uid = _ent.GetEntity(uidNet);
        if (!_ent.EntityExists(uid))
        {
            shell.WriteError("That grid does not exist.");
            return;
        }
        if (!_ent.HasComponent<MapGridComponent>(uid))
        {
            shell.WriteError("That entity is not a grid.");
            return;
        }
        if (args.Length == 3)
        {
            if (!ShuttleGridAccessSystem.TryParseGridKind(args[2], out var kind))
            {
                shell.WriteError($"Unknown grid type '{args[2]}'. Use: shuttle, station, event, shuttleai");
                return;
            }
            var gridAccess = _ent.System<ShuttleGridAccessSystem>();
            var existingKind = gridAccess.GetKind(uid);
            if (existingKind is ShuttleGridKind.Debris or ShuttleGridKind.Wrecks)
            {
                shell.WriteError("Debris and wrecks are auto-generated. Omit [type] to save as-is.");
                return;
            }
            gridAccess.TryGetShuttleGrid(uid, out var existing);
            gridAccess.EnsureGridType(uid, kind, existing);
        }
        var path = new ResPath(args[1]);
        var loader = _ent.System<MapLoaderSystem>();
        if (loader.TrySaveGrid(uid, path))
            shell.WriteLine("Save successful. Look in the user data directory.");
        else
            shell.WriteError("Save unsuccessful!");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHintOptions(
                    CompletionHelper.Components<MapGridComponent>(args[0], _ent),
                    Loc.GetString("cmd-hint-savebp-id"));
            case 2:
                return CompletionResult.FromHintOptions(
                    CompletionHelper.UserFilePath(args[1], _resource.UserData),
                    Loc.GetString("cmd-hint-savemap-path"));
            case 3:
                return CompletionResult.FromHintOptions(
                    new[] { "shuttle", "station", "event", "shuttleai" },
                    "Grid type");
        }
        return CompletionResult.Empty;
    }
}
