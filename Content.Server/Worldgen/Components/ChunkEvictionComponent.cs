using Content.Server.Worldgen.Systems;

namespace Content.Server.Worldgen.Components;

[RegisterComponent]
[Access(typeof(WorldControllerSystem))]
public sealed partial class ChunkEvictionComponent : Component
{
    [DataField]
    public TimeSpan EvictAt;
}

