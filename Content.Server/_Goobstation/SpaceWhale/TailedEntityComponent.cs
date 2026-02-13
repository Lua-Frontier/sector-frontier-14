using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server._Goobstation.SpaceWhale;

[RegisterComponent]
public sealed partial class TailedEntityComponent : Component
{
    [DataField]
    public int Amount = 3;

    [DataField(required: true)]
    public EntProtoId Prototype = default!;

    [DataField]
    public float Spacing = 1f;

    [DataField]
    public float Speed = 5f;

    [DataField]
    public float WiggleAmplitude = 0.1f;

    [DataField]
    public float WiggleFrequency = 0.1f;

    [DataField]
    public float Stiffness = 50.0f;

    [DataField]
    public float Damping = 2.0f;

    [DataField]
    public float MaxLengthMultiplier = 1.05f;

    [DataField]
    public float MinLengthMultiplier = 0.95f;

    [DataField]
    public Vector2 AnchorAOffset = new(-0.15f, 0);

    [DataField]
    public Vector2 AnchorBOffset = new(0.15f, 0);

    [DataField]
    public List<EntityUid> TailSegments = new();
}
