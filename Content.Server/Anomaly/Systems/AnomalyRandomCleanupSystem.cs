using Robust.Shared.Timing;

namespace Content.Server.Anomaly.Systems;

public sealed class AnomalyRandomCleanupSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private TimeSpan _lastCleanup = TimeSpan.Zero;
    private const float CleanupIntervalSeconds = 60f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTiming.CurTime - _lastCleanup > TimeSpan.FromSeconds(CleanupIntervalSeconds))
        {
            AnomalyRandomManager.CleanupTerminatedThreads();
            _lastCleanup = _gameTiming.CurTime;
        }
    }
}
