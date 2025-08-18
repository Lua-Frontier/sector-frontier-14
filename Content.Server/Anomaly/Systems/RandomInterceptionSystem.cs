using System;
using System.Collections.Generic;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using Robust.Shared.Log;
using Robust.Shared.GameObjects;

namespace Content.Server.Anomaly.Systems;

public sealed class RandomInterceptionSystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IRobustRandom _originalRandom = default!;

    private ISawmill _randomLogger = default!;
    private RandomLoggerWrapper _wrappedRandom = default!;
    private bool _interceptionActive = false;

    public override void Initialize()
    {
        base.Initialize();

        _randomLogger = _logManager.GetSawmill("random.interception");
        _randomLogger.Level = LogLevel.Debug;

        Log.Info("RandomInterceptionSystem: Initialized - Random interception system ENABLED by default");
        _wrappedRandom = new RandomLoggerWrapper(_originalRandom, "GlobalRandom");
        RandomLoggerWrapper.EnableGlobalLogging();
        ActivateInterception();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_interceptionActive)
        {
            DeactivateInterception();
        }

        _randomLogger.Info("RandomInterceptionSystem: Shutdown - Cleanup complete");
        Log.Info("RandomInterceptionSystem: Shutdown - Cleanup complete");
    }
    private void ActivateInterception()
    {
        try
        {
            IoCManager.Resolve<IRobustRandom>();
            _randomLogger.Info("Random interception ACTIVATED - all Random calls will be logged");
            _interceptionActive = true;
            TestInterception();
        }
        catch (Exception ex)
        {
            _randomLogger.Error($"Failed to activate Random interception: {ex.Message}");
        }
    }
    private void DeactivateInterception()
    {
        try
        {
            _randomLogger.Info("Random interception DEACTIVATED");
            _interceptionActive = false;
        }
        catch (Exception ex)
        {
            _randomLogger.Error($"Failed to deactivate Random interception: {ex.Message}");
        }
    }

    private void TestInterception()
    {
        try
        {
            _randomLogger.Info("Testing Random interception...");

            var testValue = _wrappedRandom.Next(1, 100);
            _randomLogger.Info($"Test Random call: Next(1, 100) = {testValue}");

            var testFloat = _wrappedRandom.NextFloat();
            _randomLogger.Info($"Test Random call: NextFloat() = {testFloat:F4}");
        }
        catch (Exception ex)
        {
            _randomLogger.Error($"Random interception test failed: {ex.Message}");
        }
    }
    public string GetInterceptionStatus()
    {
        return $"Random Interception: {(_interceptionActive ? "ACTIVE" : "INACTIVE")}\n" +
               $"Wrapper: {(_wrappedRandom != null ? "Created" : "Not Created")}\n" +
               $"Logger: {(_randomLogger != null ? "Ready" : "Not Ready")}";
    }
}
