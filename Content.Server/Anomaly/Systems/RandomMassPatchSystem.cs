using System;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using Robust.Shared.Log;
using Robust.Shared.GameObjects;

namespace Content.Server.Anomaly.Systems;
public sealed class RandomMassPatchSystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _logger = default!;

    public override void Initialize()
    {
        base.Initialize();

        _logger = _logManager.GetSawmill("random.mass.patch");
        _logger.Level = LogLevel.Debug;

        Log.Info("RandomMassPatchSystem: Initialized - Mass Random logging system ready");
        RandomLoggerWrapper.EnableGlobalLogging();

        _logger.Info("RandomMassPatchSystem: Global Random logging ENABLED for all systems");
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _logger.Info("RandomMassPatchSystem: Shutdown complete");
    }
    public string GetMassPatchStatus()
    {
        return $"Mass Random Patch: ACTIVE\n" +
               $"Global Logging: ENABLED\n" +
               $"All Systems: Will be logged automatically";
    }
}
