using Content.Shared._Lua.Performance;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Tick;

public sealed class ServerPerfBroadcastSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private TimeSpan _lastSent;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _time.RealTime;
        if ((now - _lastSent).TotalSeconds < 1)
            return;
        _lastSent = now;
        var ev = new ServerPerfUpdateEvent
        {
            ServerFpsAvg = (float) _time.FramesPerSecondAvg,
            ServerTickRate = _time.TickRate
        };
        RaiseNetworkEvent(ev, Filter.Empty().AddAllPlayers(_players));
    }
}


