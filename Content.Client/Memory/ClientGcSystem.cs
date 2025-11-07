using Robust.Client.Console;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Content.Shared.CCVar;

namespace Content.Client.Memory;

public sealed partial class ClientGcSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IClientConsoleHost _console = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _gcEnabled;
    private int _gcIntervalMinutes;
    private TimeSpan _nextGcAt;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CCVars.ClientGCEnabled, v =>
        {
            _gcEnabled = v;
            if (_gcEnabled) _nextGcAt = _timing.RealTime + TimeSpan.FromMinutes(ClampMinutes(_gcIntervalMinutes));
        }, true);

        _cfg.OnValueChanged(CCVars.ClientGCIntervalMinutes, v =>
        {
            _gcIntervalMinutes = ClampMinutes(v);
            if (_gcEnabled) _nextGcAt = _timing.RealTime + TimeSpan.FromMinutes(_gcIntervalMinutes);
        }, true);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.RealTime;

        if (_gcEnabled && now >= _nextGcAt)
        {
            _console.ExecuteCommand("gcf"); // Атвирнись :)
            _nextGcAt = now + TimeSpan.FromMinutes(_gcIntervalMinutes);
        }
    }

    private static int ClampMinutes(int minutes)
    {
        if (minutes < 5) return 5;
        if (minutes > 60) return 60;
        return minutes;
    }
}


