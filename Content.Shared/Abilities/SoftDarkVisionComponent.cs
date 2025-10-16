using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared.Abilities;

/// <summary>
/// Provides an always-on, subtle dark vision by enabling a small client-side light
/// around the owner. Intended to give "a bit more than nothing" visibility in darkness.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class SoftDarkVisionComponent : Component
{
    /// <summary>
    /// Radius (in tiles) where vision should feel essentially clear.
    /// A brighter inner light will be applied up to this radius.
    /// </summary>
    [DataField] public float PerfectRadius = 8f;

    /// <summary>
    /// Outer radius (in tiles) where visibility tapers off beyond perfect vision.
    /// </summary>
    [DataField] public float FalloffRadius = 12f;

    /// <summary>
    /// Light color. Keep neutral and dim by default.
    /// </summary>
    [DataField] public Color Color = Color.FromHex("#cfd6d9");

    /// <summary>
    /// Brightness for the inner light (0..1).
    /// </summary>
    [DataField] public float InnerEnergy = 0.7f;

    /// <summary>
    /// Brightness for the outer light (0..1).
    /// </summary>
    [DataField] public float OuterEnergy = 0.25f;
}
