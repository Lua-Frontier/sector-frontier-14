// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Lua.Common.CryoTimer;
using Content.Lua.Shared.CryoTimer;
using JetBrains.Annotations;

namespace Content.Client.Lua.CryoTimer;

[UsedImplicitly]
public sealed class CryoReturnTimerSystem : EntitySystem, ICryoReturnTimer
{
    public TimeSpan? CryoReturnTime { get; private set; }
    public event Action? CryoReturnReseted;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CryoReturnTimerEvent>(OnCryoReturnTimer);
    }

    private void OnCryoReturnTimer(CryoReturnTimerEvent e)
    {
        CryoReturnTime = e.Time;
        CryoReturnReseted?.Invoke();
    }
}
