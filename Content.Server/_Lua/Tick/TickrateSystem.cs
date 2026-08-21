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

        private ISawmill _sawmill = default!;

        private static readonly Gauge ServerFps = Metrics.CreateGauge(
            "robust_server_fps",
            "Server frames per second (FramesPerSecondAvg).");

        private static readonly Gauge ServerTickrate = Metrics.CreateGauge(
            "robust_server_tickrate",
            "Current server tickrate (net.tickrate).");

        private static readonly Gauge PhysicsTickrate = Metrics.CreateGauge(
            "robust_physics_target_tickrate",
            "Current physics target minimum tickrate (physics.target_minimum_tickrate).");

        private static readonly Gauge ServerHeadroom = Metrics.CreateGauge(
            "robust_server_headroom",
            "Server FPS headroom ratio (fps / net.tickrate).");

        private TimeSpan? _lowHeadroomSince;
        private TimeSpan? _goodHeadroomSince;
        private TimeSpan _lastCheck;
        private TimeSpan _lastTickrateChange;
        private bool _dynamicEnabled;
        private int _minTickrate;
        private int _maxTickrate;
        private float _checkIntervalSeconds;
        private float _lowFpsMin;
        private float _lowFpsMax;
        private float _highFpsMin;
        private float _decreaseHeadroom;
        private float _increaseHeadroom;
        private float _headroomSmooth;
        private float _changeCooldownSeconds;
        private float _decreaseDelaySeconds;
        private float _increaseDelaySeconds;
        private float _headroomEma = -1f;

        public override void Initialize()
        {
            base.Initialize();
            _sawmill = Logger.GetSawmill("tickrate.dynamic");

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
            _cfg.OnValueChanged(CLVars.NetDynamicTickHighFpsMin, value => _highFpsMin = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickDecreaseHeadroom, value => _decreaseHeadroom = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickIncreaseHeadroom, value => _increaseHeadroom = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickHeadroomSmooth, value => _headroomSmooth = value, true);
            _cfg.OnValueChanged(CLVars.NetDynamicTickChangeCooldown, value => _changeCooldownSeconds = value, true);
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

            var netTickrate = Math.Max(1, _cfg.GetCVar(CVars.NetTickrate));
            var srvfps = _time.FramesPerSecondAvg;
            var headroom = srvfps / netTickrate;
            _headroomEma = _headroomEma < 0f || _headroomSmooth <= 0f
                ? (float)headroom
                : _headroomEma + _headroomSmooth * ((float)headroom - _headroomEma);

            ServerFps.Set(srvfps);
            ServerTickrate.Set(netTickrate);
            PhysicsTickrate.Set(_cfg.GetCVar(CVars.TargetMinimumTickrate));
            ServerHeadroom.Set(_headroomEma);

            if (!_dynamicEnabled) return;

            var minTickrate = Math.Min(_minTickrate, _maxTickrate);
            var maxTickrate = Math.Max(_minTickrate, _maxTickrate);
            var decreaseHeadroom = ResolveDecreaseHeadroom(maxTickrate);
            var increaseHeadroom = Math.Max(decreaseHeadroom, ResolveIncreaseHeadroom(maxTickrate));
            var decreaseDelay = TimeSpan.FromSeconds(Math.Max(0.1f, _decreaseDelaySeconds));
            var increaseDelay = TimeSpan.FromSeconds(Math.Max(0.1f, _increaseDelaySeconds));
            var changeCooldown = TimeSpan.FromSeconds(Math.Max(0f, _changeCooldownSeconds));
            if (now - _lastTickrateChange < changeCooldown)
                return;

            if (_headroomEma <= decreaseHeadroom)
            {
                _goodHeadroomSince = null;
                if (_lowHeadroomSince == null) _lowHeadroomSince = now;
                if (now - _lowHeadroomSince >= decreaseDelay)
                {
                    var cur = _cfg.GetCVar(CVars.NetTickrate);
                    if (cur > minTickrate)
                    {
                        var step = CalcStep(decreaseHeadroom - _headroomEma);
                        SetNetTickrate(cur - step, minTickrate, maxTickrate, "low headroom");
                        _lastTickrateChange = now;
                    }
                    _lowHeadroomSince = now;
                }
            }
            else if (_headroomEma >= increaseHeadroom)
            {
                _lowHeadroomSince = null;
                if (_goodHeadroomSince == null) _goodHeadroomSince = now;
                if (now - _goodHeadroomSince >= increaseDelay)
                {
                    var cur = _cfg.GetCVar(CVars.NetTickrate);
                    if (cur < maxTickrate)
                    {
                        SetNetTickrate(cur + 1, minTickrate, maxTickrate, "recovered headroom");
                        _lastTickrateChange = now;
                    }
                    _goodHeadroomSince = now;
                }
            }
            else
            {
                _lowHeadroomSince = null;
                _goodHeadroomSince = null;
            }
        }

        private float ResolveDecreaseHeadroom(int maxTickrate)
        {
            if (_decreaseHeadroom > 0f)
                return _decreaseHeadroom;

            var fpsThreshold = Math.Max(_lowFpsMin, _lowFpsMax);
            return fpsThreshold / Math.Max(1, maxTickrate);
        }

        private float ResolveIncreaseHeadroom(int maxTickrate)
        {
            if (_increaseHeadroom > 0f)
                return _increaseHeadroom;

            return _highFpsMin / Math.Max(1, maxTickrate);
        }

        private static int CalcStep(float excess)
        {
            if (excess < 1f)
                return 1;

            return Math.Min(3, (int)Math.Ceiling(excess));
        }

        private void SetNetTickrate(int tickrate, int minTickrate, int maxTickrate, string reason)
        {
            var current = _cfg.GetCVar(CVars.NetTickrate);
            tickrate = Math.Clamp(tickrate, minTickrate, maxTickrate);
            if (tickrate == current)
                return;

            _cfg.SetCVar(CVars.NetTickrate, tickrate);
            _sawmill.Info(
                "Dynamic tickrate changed {Current}->{Tickrate} ({Reason}, headroom={Headroom:0.00})",
                current,
                tickrate,
                reason,
                _headroomEma);
        }

        private void ResetTimers()
        {
            var now = _time.RealTime;
            _lowHeadroomSince = null;
            _goodHeadroomSince = null;
            _lastCheck = now;
            _lastTickrateChange = now;
            _headroomEma = -1f;
        }
    }
}
