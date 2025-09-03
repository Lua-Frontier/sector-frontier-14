using Content.Client._Lua.Tick;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Lua.UserInterface.Controls;

public sealed class HudPerfLabel : RichTextLabel
{
    private readonly IGameTiming _gameTiming;
    private readonly ClientServerPerfSystem _serverPerf;

    public HudPerfLabel(IGameTiming gameTiming, ClientServerPerfSystem serverPerf)
    {
        _gameTiming = gameTiming;
        _serverPerf = serverPerf;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (!VisibleInTree)
            return;
        var clientFps = _gameTiming.FramesPerSecondAvg;
        var serverFps = _serverPerf.ServerFpsAvg;
        var tps = _serverPerf.ServerTickRate;
        var version = Loc.GetString("connecting-version");
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


