using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Weapons.Hitscan.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanJumpComponent : Component
{
    [DataField]
    public int Count = 3;

    [DataField]
    public float Range = 10;

    [DataField]
    public HashSet<EntityUid> IgnoredEntities = [];
}
