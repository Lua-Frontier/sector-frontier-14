using Robust.Shared.GameStates;
using System.Numerics;

namespace Content.Shared._Goobstation.SpaceWhale;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpaceWhaleComponent : Component
{
    [DataField]
    public TimeSpan SpawnTime;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public bool Idle;

    [ViewVariables]
    public Vector2 IdleDirection = Vector2.UnitX;

    [ViewVariables]
    public TimeSpan IdleRedirectAt;

    [DataField]
    public float IdleRedirectSeconds = 12f;

    [DataField]
    public float IdleSpeedFactor = 0.55f;
}
