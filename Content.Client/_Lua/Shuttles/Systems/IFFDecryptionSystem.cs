// LuaWorld/LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld/LuaCorp Contributors
// See AGPLv3.txt for details.
using System.Text;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Shuttles.Systems;

public sealed class IFFDecryptionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public const float Range = 100f;
    private static readonly TimeSpan DecryptDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RememberDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ScrambleInterval = TimeSpan.FromMilliseconds(120);
    private readonly Dictionary<(EntityUid Viewer, EntityUid Target), State> _states = new();
    public IFFDecryptResult Get(EntityUid viewerGrid, EntityUid targetGrid, string realName, float distance, bool hideLabel)
    {
        var now = _timing.CurTime;
        var key = (viewerGrid, targetGrid);
        if (!hideLabel)
        {
            if (_states.TryGetValue(key, out var existing) && existing.KnownUntil <= now) _states.Remove(key);
            return new IFFDecryptResult(IFFDecryptPhase.Plain, realName, string.Empty);
        }
        if (_states.TryGetValue(key, out var state))
        {
            if (state.KnownUntil > now)  return new IFFDecryptResult(IFFDecryptPhase.Known, state.RealName, string.Empty);
            if (state.KnownUntil != default && state.KnownUntil <= now)
            {
                _states.Remove(key);
                state = default;
            }
        }
        if (distance > Range)
        {
            if (state.Started != default && state.KnownUntil == default) _states.Remove(key);
            return new IFFDecryptResult(IFFDecryptPhase.Unknown, string.Empty, string.Empty);
        }
        if (state.Started == default)
        {
            state = new State
            {
                RealName = realName,
                Started = now,
                NextScrambleAt = now
            };
        }
        if (state.RealName != realName)
        {
            state.RealName = realName;
            state.Started = now;
            state.Cipher = string.Empty;
            state.NextScrambleAt = now;
        }
        var elapsed = now - state.Started;
        if (elapsed >= DecryptDuration)
        {
            state.KnownUntil = now + RememberDuration;
            state.Cipher = string.Empty;
            _states[key] = state;
            return new IFFDecryptResult(IFFDecryptPhase.Known, state.RealName, string.Empty);
        }
        var total = state.RealName.Length;
        if (total <= 0)
        {
            _states[key] = state;
            return new IFFDecryptResult(IFFDecryptPhase.Unknown, string.Empty, string.Empty);
        }
        var revealed = (int) MathF.Floor((float) (elapsed.TotalSeconds / DecryptDuration.TotalSeconds) * total);
        revealed = Math.Clamp(revealed, 0, total);
        var cipher = state.Cipher ?? string.Empty;
        if (now >= state.NextScrambleAt || cipher.Length != total - revealed)
        {
            cipher = BuildCipher(total - revealed);
            state.Cipher = cipher;
            state.NextScrambleAt = now + ScrambleInterval;
        }
        _states[key] = state;
        return new IFFDecryptResult(IFFDecryptPhase.Decrypting, state.RealName[..revealed], cipher);
    }
    private string BuildCipher(int len)
    {
        if (len <= 0) return string.Empty;
        const string pool = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+[]{};:,.<>?/\\|";
        var sb = new StringBuilder(len);
        for (var i = 0; i < len; i++) sb.Append(pool[_random.Next(pool.Length)]);
        return sb.ToString();
    }
    private struct State
    {
        public string RealName;
        public TimeSpan Started;
        public TimeSpan KnownUntil;
        public TimeSpan NextScrambleAt;
        public string Cipher;
    }
}

public readonly record struct IFFDecryptResult(IFFDecryptPhase Phase, string Revealed, string Cipher);

public enum IFFDecryptPhase : byte
{
    Plain,
    Unknown,
    Decrypting,
    Known
}


