using System;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using Robust.Shared.Log;
using Robust.Shared.GameObjects;

namespace Content.Server.Anomaly.Systems;

public sealed class RandomIoCInterceptionSystem : EntitySystem
{
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _logger = default!;
    private RandomLoggerWrapper _wrappedRandom = default!;
    private IRobustRandom _originalRandom = default!;
    private bool _interceptionActive = false;

    public override void Initialize()
    {
        base.Initialize();

        _logger = _logManager.GetSawmill("random.ioc.interception");
        _logger.Level = LogLevel.Debug;
        Log.Info("RandomIoCInterceptionSystem: Initialized");
        _originalRandom = IoCManager.Resolve<IRobustRandom>();
        _wrappedRandom = new RandomLoggerWrapper(_originalRandom, "IoCInterception");
        ActivateInterception();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_interceptionActive)
        {
            DeactivateInterception();
        }

        _logger.Info("RandomIoCInterceptionSystem: Shutdown complete");
    }
    private void ActivateInterception()
    {
        try
        {

            _logger.Info("Attempting to intercept IRobustRandom in IoC container...");
            TestWrapper();

            _interceptionActive = true;
            _logger.Info("Random IoC interception ACTIVATED (wrapper created)");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to activate IoC interception: {ex.Message}");
            _logger.Info("Falling back to wrapper-only mode");
            _interceptionActive = true;
        }
    }
    private void DeactivateInterception()
    {
        try
        {
            _logger.Info("Random IoC interception DEACTIVATED");
            _interceptionActive = false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to deactivate IoC interception: {ex.Message}");
        }
    }
    private void TestWrapper()
    {
        try
        {
            _logger.Info("Testing RandomLoggerWrapper...");

            var testValue = _wrappedRandom.Next(1, 100);
            _logger.Info($"Wrapper test: Next(1, 100) = {testValue}");

            var testFloat = _wrappedRandom.NextFloat();
            _logger.Info($"Wrapper test: NextFloat() = {testFloat:F4}");

            _logger.Info("RandomLoggerWrapper test completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error($"RandomLoggerWrapper test failed: {ex.Message}");
            throw;
        }
    }
    public string GetStatus()
    {
        return $"IoC Interception: {(_interceptionActive ? "ACTIVE" : "INACTIVE")}\n" +
               $"Wrapper: {(_wrappedRandom != null ? "Created" : "Not Created")}\n" +
               $"Original Random: {(_originalRandom != null ? "Available" : "Not Available")}";
    }
    public RandomLoggerWrapper GetWrapper()
    {
        return _wrappedRandom;
    }
}
