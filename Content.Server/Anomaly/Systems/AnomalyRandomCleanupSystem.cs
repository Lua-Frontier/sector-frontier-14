using System;
using Robust.Shared.Timing;
using Robust.Shared.Log;

namespace Content.Server.Anomaly.Systems;

public sealed class AnomalyRandomCleanupSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private TimeSpan _lastCleanup = TimeSpan.Zero;
    private TimeSpan _lastDiagnostics = TimeSpan.Zero;
    private const float CleanupIntervalSeconds = 60f;
    private const float DiagnosticsIntervalSeconds = 30f;

    private ISawmill _logger = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logger = _logManager.GetSawmill("anomaly.random.cleanup");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;

        if (curTime - _lastCleanup > TimeSpan.FromSeconds(CleanupIntervalSeconds))
        {
            AnomalyRandomManager.CleanupTerminatedThreads();
            _lastCleanup = curTime;

            _logger.Debug("AnomalyRandomCleanupSystem: Performed regular cleanup");
        }

        if (curTime - _lastDiagnostics > TimeSpan.FromSeconds(DiagnosticsIntervalSeconds))
        {
            PerformRandomDiagnostics();
            _lastDiagnostics = curTime;
        }
    }

    private void PerformRandomDiagnostics()
    {
        try
        {
            if (AnomalyRandomManager.IsGlobalRandomCorrupted())
            {
                _logger.Warning("AnomalyRandomCleanupSystem: Global Random corruption detected! Attempting recovery...");

                AnomalyRandomManager.ResetGlobalRandom();

                var diagnostics = AnomalyRandomManager.GetDetailedDiagnostics();
                _logger.Info($"AnomalyRandomCleanupSystem: Random diagnostics after recovery:\n{diagnostics}");
            }
            else
            {
                var diagnostics = AnomalyRandomManager.GetDetailedDiagnostics();
                _logger.Debug($"AnomalyRandomCleanupSystem: Random diagnostics (normal):\n{diagnostics}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"AnomalyRandomCleanupSystem: Error during Random diagnostics: {ex.Message}");
        }
    }
}
