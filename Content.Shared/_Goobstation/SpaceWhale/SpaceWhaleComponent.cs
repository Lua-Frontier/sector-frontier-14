using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.SpaceWhale;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpaceWhaleComponent : Component
{
    [DataField]
    public TimeSpan SpawnTime;

    [ViewVariables]
    public EntityUid? Target;
}
