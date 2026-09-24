using Content.Server.Worldgen.Systems;

namespace Content.Server.Worldgen.Components;

[RegisterComponent]
[Access(typeof(WorldControllerSystem), Other = AccessPermissions.ReadWriteExecute)]
public sealed partial class ChunkEvictionComponent : Component
{
    [DataField]
    public TimeSpan EvictAt;
}
