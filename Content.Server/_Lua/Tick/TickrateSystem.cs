// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using JetBrains.Annotations;
using Content.Shared.Lua.CLVar;
using Prometheus;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Tick
{
    [UsedImplicitly]
    public sealed class TickrateSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IGameTiming _time = default!;

        private static readonly Gauge ServerFps = Metrics.CreateGauge(
            "robust_server_fps",
            "Server frames per second (FramesPerSecondAvg).");

        private static readonly Gauge ServerTickrate = Metrics.CreateGauge(
            "robust_server_tickrate",
            "Current server tickrate (net.tickrate).");

        private TimeSpan? _lowFpsSince;
        private TimeSpan _lastLowFps;
        private TimeSpan _lastIncrease;
        private TimeSpan _lastCheck;
        private bool _dynamicEnabled;
        private int _minTickrate;
        private int _maxTickrate;
        private float _checkIntervalSeconds;
        private float _lowFpsMin;
        private float _lowFpsMax;
        private float _decreaseDelaySeconds;
        private float _increaseDelaySeconds;

        public override void Initialize()
        {
            base.Initialize();
            _cfg.OnValueChanged(CLVars.NetDynamicTick, dynamicEnabled =>
            {
                _dynamicEnabled = dynamicEnabled;
                ResetTimers();
            }, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickMinTickrate, value => _minTickrate = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickMaxTickrate, value => _maxTickrate = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickCheckInterval, value => _checkIntervalSeconds = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickLowFpsMin, value => _lowFpsMin = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickLowFpsMax, value => _lowFpsMax = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickDecreaseDelay, value => _decreaseDelaySeconds = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickIncreaseDelay, value => _increaseDelaySeconds = value, true);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var now = _time.RealTime;
            var checkInterval = TimeSpan.FromSeconds(Math.Max(0.1f, _checkIntervalSeconds));
            if (now - _lastCheck < checkInterval) return;
            _lastCheck = now;
            var srvfps = _time.FramesPerSecondAvg;
            ServerFps.Set(srvfps);
            ServerTickrate.Set(_cfg.GetCVar(CVars.NetTickrate));
            if (!_dynamicEnabled) return;
            var minTickrate = Math.Min(_minTickrate, _maxTickrate);
            var maxTickrate = Math.Max(_minTickrate, _maxTickrate);
            var lowFpsMin = Math.Min(_lowFpsMin, _lowFpsMax);
            var lowFpsMax = Math.Max(_lowFpsMin, _lowFpsMax);
            var decreaseDelay = TimeSpan.FromSeconds(Math.Max(0.1f, _decreaseDelaySeconds));
            var increaseDelay = TimeSpan.FromSeconds(Math.Max(0.1f, _increaseDelaySeconds));
            if (srvfps >= lowFpsMin && srvfps <= lowFpsMax)
            {
                if (_lowFpsSince == null) _lowFpsSince = now;
                if (now - _lowFpsSince >= decreaseDelay)
                {
                    var cur = _cfg.GetCVar(CVars.NetTickrate);
                    if (cur > minTickrate) _cfg.SetCVar(CVars.NetTickrate, cur - 1);
                    _lowFpsSince = now;
                }
                _lastLowFps = now;
            }
            else
            { _lowFpsSince = null; }

            if (now - _lastLowFps >= increaseDelay && now - _lastIncrease >= increaseDelay)
            {
                var cur = _cfg.GetCVar(CVars.NetTickrate);
                if (cur < maxTickrate) _cfg.SetCVar(CVars.NetTickrate, cur + 1);
                _lastIncrease = now;
            }
        }

        private void ResetTimers()
        {
            var now = _time.RealTime;
            _lowFpsSince = null;
            _lastLowFps = now;
            _lastIncrease = now;
            _lastCheck = now;
        }
    }
}
