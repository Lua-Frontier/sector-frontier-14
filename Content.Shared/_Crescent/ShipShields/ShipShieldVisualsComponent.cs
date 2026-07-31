using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Crescent.ShipShields;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipShieldVisualsComponent : Component
{
    /// <summary>
    /// The color of this shield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color ShieldColor = Color.FromHex("#00AAFF").WithAlpha(0.92f);

    /// <summary>
    /// The extra padding of this shield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Padding = 50f;
    // Lua personal shield to ship shield start
    [ViewVariables, AutoNetworkedField]
    public float Form;

    [ViewVariables, AutoNetworkedField]
    public float Shatter;

    [DataField]
    public float SpinupTime = 1.25f;

    [DataField]
    public float ShatterTime = 1.0f;

    [DataField]
    public float Brightness = 1.15f;

    [DataField]
    public float PixelGrid = 1f;

    [DataField]
    public float HexDensity = 14f;

    [DataField]
    public float CoreFade = 0.83f;

    [DataField]
    public float FillLevel = 0.12f;

    [DataField]
    public float LineLevel = 0.48f;

    [DataField]
    public float RimLevel = 0.8f;

    [DataField]
    public float AlphaBands = 6f;

    [DataField]
    public float BreathDepth = 0.1f;

    [DataField]
    public Vector2 FormOrigin = Vector2.Zero;

    [DataField]
    public float ShardScale = 5f;
    // Lua personal shield to ship shield end
}
