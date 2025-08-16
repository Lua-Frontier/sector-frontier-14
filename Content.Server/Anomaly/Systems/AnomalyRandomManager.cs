using System;
using System.Collections.Concurrent;
using System.Threading;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Shared.IoC;
using System.Collections.Generic;

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
}
