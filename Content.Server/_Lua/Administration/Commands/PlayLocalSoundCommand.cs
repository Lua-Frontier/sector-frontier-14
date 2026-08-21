// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;

namespace Content.Server._Lua.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class PlayLocalSoundCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    public string Command => "playlocalsound";
    public string Description => Loc.GetString("play-local-sound-command-description");
    public string Help => Loc.GetString("play-local-sound-command-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        var path = args[0];
        var audio = AudioParams.Default;
        var index = 1;
        var rangeArg = "sector";

        if (args.Length > index && int.TryParse(args[index], out var volume))
        {
            audio = audio.WithVolume(volume);
            index++;
        }

        if (args.Length > index &&
            (string.Equals(args[index], "sector", StringComparison.OrdinalIgnoreCase)
             || float.TryParse(args[index], out _)))
        {
            rangeArg = args[index];
            index++;
        }
        else if (args.Length > index && !IsLikelyUsernameStart(args, index))
        {
            shell.WriteError(Loc.GetString("play-local-sound-command-range-parse", ("range", args[index])));
            return;
        }

        List<ICommonSession>? users = null;
        if (args.Length > index)
        {
            users = new List<ICommonSession>();
            for (var i = index; i < args.Length; i++)
            {
                if (!_playerManager.TryGetSessionByUsername(args[i], out var session))
                {
                    shell.WriteError(Loc.GetString("play-local-sound-command-player-not-found", ("username", args[i])));
                    continue;
                }

                users.Add(session);
            }
        }

        audio = audio.AddVolume(-8);
        PlayAtOrigin(shell, path, audio, rangeArg, users);
    }

    private static bool IsLikelyUsernameStart(string[] args, int index)
    {
        return !string.Equals(args[index], "sector", StringComparison.OrdinalIgnoreCase)
               && !float.TryParse(args[index], out _)
               && !int.TryParse(args[index], out _);
    }

    private void PlayAtOrigin(IConsoleShell shell, string path, AudioParams audio, string rangeArg, List<ICommonSession>? users)
    {
        EntityUid? origin = null;
        if (shell.Player?.AttachedEntity is { } playerEnt)
            origin = playerEnt;
        else if (users is { Count: > 0 } && users[0].AttachedEntity is { } firstUserEnt)
            origin = firstUserEnt;

        if (origin == null || !_entManager.TryGetComponent(origin.Value, out TransformComponent? xform))
        {
            shell.WriteError(Loc.GetString("play-local-sound-command-no-origin"));
            return;
        }

        var xformSys = _entManager.System<SharedTransformSystem>();
        EntityCoordinates playAt;
        if (xform.GridUid is { Valid: true } gridUid)
        {
            var local = Vector2.Transform(xformSys.GetWorldPosition(origin.Value), xformSys.GetInvWorldMatrix(gridUid));
            playAt = new EntityCoordinates(gridUid, local);
        }
        else if (xform.MapUid is { Valid: true } mapUid)
        {
            playAt = new EntityCoordinates(mapUid, xformSys.GetWorldPosition(origin.Value));
        }
        else
        {
            shell.WriteError(Loc.GetString("play-local-sound-command-no-origin"));
            return;
        }

        Filter filter;
        if (string.Equals(rangeArg, "sector", StringComparison.OrdinalIgnoreCase))
        {
            filter = Filter.Empty().AddInMap(xform.MapID, _entManager);
        }
        else if (float.TryParse(rangeArg, out var radius) && radius > 0)
        {
            audio = audio.WithMaxDistance(radius);
            var mapCoords = xformSys.ToMapCoordinates(playAt);
            filter = Filter.Empty().AddInRange(mapCoords, radius, _playerManager, _entManager);
        }
        else
        {
            shell.WriteError(Loc.GetString("play-local-sound-command-range-parse", ("range", rangeArg)));
            return;
        }

        if (users != null)
        {
            var allowed = users.ToHashSet();
            var restricted = Filter.Empty();
            foreach (var session in filter.Recipients)
            {
                if (allowed.Contains(session))
                    restricted.AddPlayer(session);
            }

            filter = restricted;
        }

        var replay = users == null;
        _entManager.System<SharedAudioSystem>().PlayStatic(path, filter, playAt, replay, audio);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.AudioFilePath(args[0], _protoManager, _res),
                Loc.GetString("play-local-sound-command-arg-path"));
        }

        if (args.Length == 2)
            return CompletionResult.FromHint(Loc.GetString("play-local-sound-command-arg-volume"));

        if (args.Length == 3)
        {
            var options = new[] { "sector" };
            return CompletionResult.FromHintOptions(options, Loc.GetString("play-local-sound-command-arg-range"));
        }

        if (args.Length > 3)
        {
            var options = _playerManager.Sessions.Select(c => c.Name);
            return CompletionResult.FromHintOptions(
                options,
                Loc.GetString("play-local-sound-command-arg-usern", ("user", args.Length - 3)));
        }

        return CompletionResult.Empty;
    }
}
