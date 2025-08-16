using System;
using System.Collections.Concurrent;
using System.Threading;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Shared.IoC;
using System.Collections.Generic;
using System.Linq;

namespace Content.Server.Anomaly.Systems;

public sealed class AnomalyRandomManager
{
    private static readonly ConcurrentDictionary<int, IRobustRandom> _threadRandoms = new();
    private static readonly object _globalLock = new object();
    private static IRobustRandom? _fallbackRandom;

    public static IRobustRandom GetThreadRandom()
    {
        var threadId = Thread.CurrentThread.ManagedThreadId;

        if (_threadRandoms.TryGetValue(threadId, out var existingRandom))
            return existingRandom;

        var newRandom = CreateThreadSafeRandom();
        _threadRandoms[threadId] = newRandom;
        return newRandom;
    }

    private static IRobustRandom CreateThreadSafeRandom()
    {
        lock (_globalLock)
        {
            if (_fallbackRandom == null)
            {
                _fallbackRandom = IoCManager.Resolve<IRobustRandom>();
            }

            var seed = _fallbackRandom.Next();
            var newRandom = new RobustRandom();
            newRandom.SetSeed(seed);
            return newRandom;
        }
    }

    public static void CleanupTerminatedThreads()
    {
        var currentThreadId = Thread.CurrentThread.ManagedThreadId;

        var threadsToRemove = new List<int>();
        foreach (var threadId in _threadRandoms.Keys)
        {
            if (threadId != currentThreadId)
                threadsToRemove.Add(threadId);
        }

        foreach (var threadId in threadsToRemove)
        {
            _threadRandoms.TryRemove(threadId, out _);
        }
    }

    public static string GetStatistics()
    {
        return $"Active thread Randoms: {_threadRandoms.Count}, Fallback Random: {(_fallbackRandom != null ? "Available" : "Not Available")}";
    }

    public static void ResetAllThreadRandoms()
    {
        lock (_globalLock)
        {
            _threadRandoms.Clear();
            _fallbackRandom = null;
        }
    }

    // LUA DEBUG: New diagnostic methods
    public static string GetDetailedDiagnostics()
    {
        var diagnostics = new List<string>();

        if (_fallbackRandom != null)
        {
            try
            {
                var testValue = _fallbackRandom.Next(1, 100);
                diagnostics.Add($"Global Random: Available, Test value: {testValue}");
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Global Random: ERROR - {ex.Message}");
            }
        }
        else
        {
            diagnostics.Add("Global Random: Not Available");
        }

        diagnostics.Add($"Thread Randoms: {_threadRandoms.Count} active");
        foreach (var (threadId, random) in _threadRandoms)
        {
            try
            {
                var testValue = random.Next(1, 100);
                diagnostics.Add($"  Thread {threadId}: Test value: {testValue}");
            }
            catch (Exception ex)
            {
                diagnostics.Add($"  Thread {threadId}: ERROR - {ex.Message}");
            }
        }

        return string.Join("\n", diagnostics);
    }

    public static bool IsGlobalRandomCorrupted()
    {
        if (_fallbackRandom == null)
            return false;

        try
        {
            var values = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                values.Add(_fallbackRandom.Next(1, 100));
            }

            if (values.All(v => v == values[0]))
                return true;

            if (values.Zip(values.Skip(1), (a, b) => b - a).All(diff => diff == 1))
                return true;

            return false;
        }
        catch
        {
            return true;
        }
    }

    public static void ResetGlobalRandom()
    {
        lock (_globalLock)
        {
            _fallbackRandom = null;
            Console.WriteLine("AnomalyRandomManager: Reset global Random");
        }
    }
}
