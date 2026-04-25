using Content.Shared.FixedPoint;

namespace Content.Shared.Fluids.Components;

[RegisterComponent]
public sealed partial class DeleteWhenDrainedComponent : Component
{
    [DataField]
    public FixedPoint2 Threshold = FixedPoint2.New(100);
}
