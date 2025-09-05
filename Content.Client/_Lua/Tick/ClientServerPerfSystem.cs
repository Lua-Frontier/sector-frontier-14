using Content.Shared._Lua.Performance;

namespace Content.Client._Lua.Tick;

public sealed class ClientServerPerfSystem : EntitySystem
{
    public float ServerFpsAvg { get; private set; }
    public ushort ServerTickRate { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ServerPerfUpdateEvent>(OnPerfUpdate);
    }

    private void OnPerfUpdate(ServerPerfUpdateEvent ev)
    {
        ServerFpsAvg = ev.ServerFpsAvg;
        ServerTickRate = ev.ServerTickRate;
    }
}


