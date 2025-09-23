// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using JetBrains.Annotations;
using Content.Shared.Lua.CLVar;
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

        private TimeSpan? _lowFpsSince;
        private TimeSpan _lastLowFps;
        private TimeSpan _lastIncrease;
        private const int MinTickrate = 15;
        private const int MaxTickrate = 30;

        public override void Initialize()
        {
            base.Initialize();
            _cfg.OnValueChanged(CLVars.NetDynamicTick, dynamicEnabled =>
            {
                _lowFpsSince = null;
                _lastLowFps = _time.RealTime;
                _lastIncrease = _time.RealTime;
            }, true);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            if (!_cfg.GetCVar(CLVars.NetDynamicTick)) return;
            var now = _time.RealTime;
            var srvfps = _time.FramesPerSecondAvg;
            var srvfpsRounded = (int) Math.Round(srvfps);
            if (srvfpsRounded >= 4 && srvfpsRounded <= 9)
            {
                if (_lowFpsSince == null) _lowFpsSince = now;
                if (now - _lowFpsSince >= TimeSpan.FromSeconds(15))
                {
                    var cur = _cfg.GetCVar(CVars.NetTickrate);
                    if (cur > MinTickrate) _cfg.SetCVar(CVars.NetTickrate, cur - 1);
                    _lowFpsSince = now;
                }
                _lastLowFps = now;
            }
            else
            { _lowFpsSince = null; }

            if (now - _lastLowFps >= TimeSpan.FromMinutes(20) && now - _lastIncrease >= TimeSpan.FromMinutes(20))
            {
                var cur = _cfg.GetCVar(CVars.NetTickrate);
                if (cur < MaxTickrate) _cfg.SetCVar(CVars.NetTickrate, cur + 1);
                _lastIncrease = now;
            }
        }
    }
}

// Experimental function
