using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.CCVar;
using Robust.Server;
using Robust.Server.Player;
using Robust.Server.ServerStatus;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.ServerUpdates;

/// <summary>
/// Responsible for restarting the server for update, when not disruptive.
/// </summary>
public sealed class ServerUpdateManager
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IWatchdogApi _watchdog = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IBaseServer _server = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    [ViewVariables]
    private bool _updateOnRoundEnd;

    private TimeSpan? _restartTime;

    public void Initialize()
    {
        _watchdog.UpdateReceived += WatchdogOnUpdateReceived;
        _playerManager.PlayerStatusChanged += PlayerManagerOnPlayerStatusChanged;
    }

    public void Update()
    {
        if (_restartTime != null && _restartTime < _gameTiming.RealTime)
        {
            DoShutdown(_updateOnRoundEnd);
        }
    }

    /// <summary>
    /// Notify that the round just ended, which is a great time to restart if necessary!
    /// </summary>
    /// <returns>True if the server is going to restart.</returns>
    public bool RoundEnded()
    {
        if (_updateOnRoundEnd)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("server-updates-shutdown"));
            DoShutdown(true);
            return true;
        }

        return false;
    }

    private void PlayerManagerOnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Connecting:
                _restartTime = null;
                break;
            case SessionStatus.Disconnected:
                break;
        }
    }

    private void WatchdogOnUpdateReceived()
    {
        _chatManager.DispatchServerAnnouncement(Loc.GetString("server-updates-received"));
        _updateOnRoundEnd = true;

        if (!_playerManager.Sessions.Any(p => p.Status != SessionStatus.Disconnected))
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("server-restart-round-ended"));
        }
        //else
        //{
        //    // No players online, schedule immediate restart.
        //    var restartDelay = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.UpdateRestartDelay));
        //    _restartTime = restartDelay + _gameTiming.RealTime;
        //}
    }

    private void DoShutdown(bool isUpdate)
    {
        var shutdownMessage = isUpdate
            ? Loc.GetString("server-updates-shutdown")
            : Loc.GetString("server-restart-round-ended");

        _chatManager.DispatchServerAnnouncement(shutdownMessage);
        _server.Shutdown(shutdownMessage);
    }

    [AdminCommand(AdminFlags.Server)]
    public sealed class CheckUpdateCommand : IConsoleCommand
    {
        public string Command => "checkupdate";
        public string Description => "Проверить наличие обновлений";
        public string Help => "Используйте: checkupdate";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (IoCManager.Resolve<ServerUpdateManager>()._updateOnRoundEnd)
            {
                shell.WriteLine("Сервер в очереди на перезагрузку для обновления");
            }
            else
            {
                shell.WriteLine("Сервер не получал обновлений");
            }
        }
    }

    [AdminCommand(AdminFlags.Server)]
    public sealed class TriggerFakeUpdateCommand : IConsoleCommand
    {
        public string Command => "triggerfakeupdate";
        public string Description => "Симуляция обновления";
        public string Help => "Используйте: triggerfakeupdate";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            shell.WriteLine("Симуляция обновления...");
            IoCManager.Resolve<ServerUpdateManager>().WatchdogOnUpdateReceived();
        }
    }
}
