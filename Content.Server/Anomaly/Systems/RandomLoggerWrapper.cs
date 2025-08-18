using System;
using System.Diagnostics;
using Robust.Shared.Random;
using Robust.Shared.Log;
using Robust.Shared.IoC;

namespace Content.Server.Anomaly.Systems;
public sealed class RandomLoggerWrapper : IRobustRandom
{
    private readonly IRobustRandom _wrappedRandom;
    private readonly string _callerSystem;
    private static RandomLoggerWrapper? _globalWrapper;
    private static bool _globalLoggingEnabled = false;

    public RandomLoggerWrapper(IRobustRandom wrappedRandom, string callerSystem)
    {
        _wrappedRandom = wrappedRandom;
        _callerSystem = callerSystem;
    }
    public static void EnableGlobalLogging()
    {
        if (_globalWrapper == null)
        {
            var globalRandom = IoCManager.Resolve<IRobustRandom>();
            _globalWrapper = new RandomLoggerWrapper(globalRandom, "GlobalRandom");
        }

        _globalLoggingEnabled = true;
        Console.WriteLine("[RandomLogger] Global Random logging ENABLED");
    }
    public static void DisableGlobalLogging()
    {
        _globalLoggingEnabled = false;
        Console.WriteLine("[RandomLogger] Global Random logging DISABLED");
    }
    public static void LogGlobalRandomCall(string methodCall, string? warning = null)
    {
        if (!_globalLoggingEnabled) return;

        try
        {
            var stackTrace = new StackTrace(2, true);
            var callerFrame = stackTrace.GetFrame(0);
            var callerMethod = callerFrame?.GetMethod();
            var callerType = callerMethod?.DeclaringType;
            var callerName = callerType?.Name ?? "Unknown";

            var logMessage = $"[GlobalRandomLogger] {callerType?.FullName}.{callerMethod?.Name}() calls {methodCall}";

            if (warning != null)
            {
                Console.WriteLine($"[WARNING] {logMessage} | {warning}");
            }
            else
            {
                Console.WriteLine($"[DEBUG] {logMessage}");
            }
        }
        catch
        {
            Console.WriteLine($"[GlobalRandomLogger] Unknown caller calls {methodCall}");
        }
    }

    public static void LogRandomCallFromSystem(string systemName, string methodCall, string? warning = null)
    {
        if (!_globalLoggingEnabled) return;

        var logMessage = $"[SystemRandomLogger] {systemName} calls {methodCall}";

        if (warning != null)
        {
            Console.WriteLine($"[WARNING] {logMessage} | {warning}");
        }
        else
        {
            Console.WriteLine($"[DEBUG] {logMessage}");
        }
    }

    public System.Random GetRandom()
    {
        LogRandomCall("GetRandom()");
        LogGlobalRandomCall("GetRandom()");
        return _wrappedRandom.GetRandom();
    }

    public void SetSeed(int seed)
    {
        LogRandomCall($"SetSeed({seed})", "CRITICAL: SetSeed called - this may corrupt global Random!");
        LogGlobalRandomCall($"SetSeed({seed})", "CRITICAL: SetSeed called - this may corrupt global Random!");
        _wrappedRandom.SetSeed(seed);
    }

    public float NextFloat()
    {
        var result = _wrappedRandom.NextFloat();
        LogRandomCall($"NextFloat() = {result:F4}");
        LogGlobalRandomCall($"NextFloat() = {result:F4}");
        return result;
    }

    public float NextFloat(float maxValue)
    {
        var result = _wrappedRandom.NextFloat(maxValue);
        LogRandomCall($"NextFloat({maxValue}) = {result:F4}");
        LogGlobalRandomCall($"NextFloat({maxValue}) = {result:F4}");
        return result;
    }

    public float NextFloat(float minValue, float maxValue)
    {
        var result = _wrappedRandom.NextFloat(minValue, maxValue);
        LogRandomCall($"NextFloat({minValue}, {maxValue}) = {result:F4}");
        LogGlobalRandomCall($"NextFloat({minValue}, {maxValue}) = {result:F4}");
        return result;
    }

    public int Next()
    {
        var result = _wrappedRandom.Next();
        LogRandomCall($"Next() = {result}");
        LogGlobalRandomCall($"Next() = {result}");
        return result;
    }

    public int Next(int maxValue)
    {
        var result = _wrappedRandom.Next(maxValue);
        LogRandomCall($"Next({maxValue}) = {result}");
        LogGlobalRandomCall($"Next({maxValue}) = {result}");
        return result;
    }

    public int Next(int minValue, int maxValue)
    {
        var result = _wrappedRandom.Next(minValue, maxValue);
        LogRandomCall($"Next({minValue}, {maxValue}) = {result}");
        LogGlobalRandomCall($"Next({minValue}, {maxValue}) = {result}");
        return result;
    }

    public double NextDouble()
    {
        var result = _wrappedRandom.NextDouble();
        LogRandomCall($"NextDouble() = {result:F4}");
        LogGlobalRandomCall($"NextDouble() = {result:F4}");
        return result;
    }

    public void NextBytes(byte[] buffer)
    {
        LogRandomCall($"NextBytes(buffer[{buffer.Length}])");
        LogGlobalRandomCall($"NextBytes(buffer[{buffer.Length}])");
        _wrappedRandom.NextBytes(buffer);
    }

    public TimeSpan Next(TimeSpan maxTime)
    {
        var result = _wrappedRandom.Next(maxTime);
        LogRandomCall($"Next(TimeSpan {maxTime}) = {result}");
        LogGlobalRandomCall($"Next(TimeSpan {maxTime}) = {result}");
        return result;
    }

    public TimeSpan Next(TimeSpan minTime, TimeSpan maxTime)
    {
        var result = _wrappedRandom.Next(minTime, maxTime);
        LogRandomCall($"Next(TimeSpan {minTime}, {maxTime}) = {result}");
        LogGlobalRandomCall($"Next(TimeSpan {minTime}, {maxTime}) = {result}");
        return result;
    }

    private void LogRandomCall(string methodCall, string? warning = null)
    {
        try
        {
            var stackTrace = new StackTrace(2, true);
            var callerFrame = stackTrace.GetFrame(0);
            var callerMethod = callerFrame?.GetMethod();
            var callerType = callerMethod?.DeclaringType;
            var callerName = callerType?.Name ?? "Unknown";

            var logMessage = $"[RandomLogger] {_callerSystem} -> {callerName}.{callerMethod?.Name}() calls {methodCall}";

            if (warning != null)
            {
                Console.WriteLine($"[WARNING] {logMessage} | {warning}");
            }
            else
            {
                Console.WriteLine($"[DEBUG] {logMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RandomLogger] {_callerSystem} calls {methodCall}");
        }
    }
}
