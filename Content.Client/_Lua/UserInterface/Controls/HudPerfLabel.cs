using Content.Client._Lua.Tick;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Lua.UserInterface.Controls;

public sealed class HudPerfLabel : RichTextLabel
{
    private readonly IGameTiming _gameTiming;
    private readonly ClientServerPerfSystem _serverPerf;
    private readonly IConfigurationManager _cfg;

    public HudPerfLabel(IGameTiming gameTiming, ClientServerPerfSystem serverPerf, IConfigurationManager cfg)
    {
        _gameTiming = gameTiming;
        _serverPerf = serverPerf;
        _cfg = cfg;
    }

    private string FormatVersion()
    {
        var engineVersion = _cfg.GetCVar(CVars.BuildEngineVersion);
        var buildVersion = _cfg.GetCVar(CVars.BuildVersion);
        var parts = engineVersion.Split('.');
        var shortEngine = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : engineVersion;
        if (string.IsNullOrEmpty(buildVersion))
            return $"Beta v{shortEngine}";
        var ts = buildVersion.Length >= 12
            ? buildVersion[..4] + buildVersion[8..12]
            : buildVersion;
        return $"Beta v{shortEngine}_{ts}";
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (!VisibleInTree)
            return;
        var clientFps = _gameTiming.FramesPerSecondAvg;
        var serverFps = _serverPerf.ServerFpsAvg;
        var tps = _serverPerf.ServerTickRate;
        var version = FormatVersion();
        string statusText;
        string statusColorHex;
        if (serverFps < 50)
        {
            statusText = Loc.GetString("server-status-high");
            statusColorHex = "#FF0000";
        }
        else if (serverFps < 150)
        {
            statusText = Loc.GetString("server-status-medium");
            statusColorHex = "#FFFF00";
        }
        else
        {
            statusText = Loc.GetString("server-status-stable");
            statusColorHex = "#00FF00";
        }
        Text = $"FPS: {clientFps:N0} | SrvFPS: [color={statusColorHex}]{serverFps:N0}[/color] | TPS: {tps} | {version} | [color={statusColorHex}]{statusText}[/color]";
    }
}


