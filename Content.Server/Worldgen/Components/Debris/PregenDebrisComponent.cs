using Content.Server.Worldgen.Systems;
using Content.Server.Worldgen.Systems.Debris;

namespace Content.Server.Worldgen.Components.Debris;

[RegisterComponent]
[Access(typeof(DebrisFeaturePlacerSystem), typeof(LocalityLoaderSystem))]
public sealed partial class PregenDebrisComponent : Component
{
    [ViewVariables]
    public bool AwaitingLocality = true;
}
