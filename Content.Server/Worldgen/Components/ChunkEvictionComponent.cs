using Content.Server._Lua.Administration.UI;
using Content.Server.Worldgen.Systems;

namespace Content.Server.Worldgen.Components;

[RegisterComponent]
[Access(typeof(WorldControllerSystem), typeof(ChunkMonitorEui))]
public sealed partial class ChunkEvictionComponent : Component
{
    [DataField]
    public TimeSpan EvictAt;
}

