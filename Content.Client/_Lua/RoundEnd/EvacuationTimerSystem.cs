// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.RoundEnd;

namespace Content.Client._Lua.RoundEnd;

public sealed class EvacuationTimerSystem : EntitySystem
{
    public TimeSpan? ExpectedEvacuationTime { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<EvacuationTimerUpdateEvent>(OnEvacuationTimerUpdate);
    }

    private void OnEvacuationTimerUpdate(EvacuationTimerUpdateEvent ev)
    {
        ExpectedEvacuationTime = ev.ExpectedEvacuationTime;
    }
}
