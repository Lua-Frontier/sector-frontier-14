using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Melee.EnergySword;

[RegisterComponent, NetworkedComponent, Access(typeof(EnergySwordSystem))]
[AutoGenerateComponentState]
public sealed partial class EnergySwordComponent : Component
{
    /// <summary>
    /// What color the blade will be when activated.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color ActivatedColor = Color.DodgerBlue;

    /// <summary>
    ///     A color option list for the random color picker.
    /// </summary>
    [DataField]
    public List<Color> ColorOptions = new()
    {
        // Lua start ЛГБТ мечи
        Color.AliceBlue,
        Color.AntiqueWhite,
        Color.Aqua,
        Color.Aquamarine,
        Color.Azure,
        Color.Beige,
        Color.Bisque,
        Color.Black,
        Color.BlanchedAlmond,
        Color.Blue,
        Color.BlueViolet,
        Color.Brown,
        Color.BurlyWood,
        Color.CadetBlue,
        Color.Chartreuse,
        Color.Chocolate,
        Color.Coral,
        Color.CornflowerBlue,
        Color.Cornsilk,
        Color.Crimson,
        Color.Cyan,
        Color.DarkBlue,
        Color.DarkCyan,
        Color.DarkGoldenrod,
        Color.DarkGray,
        Color.DarkGreen,
        Color.DarkKhaki,
        Color.DarkMagenta,
        Color.DarkOliveGreen,
        Color.DarkOrange,
        Color.DarkOrchid,
        Color.DarkRed,
        Color.DarkSalmon,
        Color.DarkSeaGreen,
        Color.DarkSlateBlue,
        Color.DarkSlateGray,
        Color.DarkTurquoise,
        Color.DarkViolet,
        Color.DeepPink,
        Color.DeepSkyBlue,
        Color.DimGray,
        Color.DodgerBlue,
        Color.Firebrick,
        Color.FloralWhite,
        Color.ForestGreen,
        Color.Fuchsia,
        Color.Gainsboro,
        Color.GhostWhite,
        Color.Gold,
        Color.Goldenrod,
        Color.Gray,
        Color.Green,
        Color.GreenYellow,
        Color.Honeydew,
        Color.HotPink,
        Color.IndianRed,
        Color.Indigo,
        Color.Ivory,
        Color.Khaki,
        Color.Lavender,
        Color.LavenderBlush,
        Color.LawnGreen,
        Color.LemonChiffon,
        Color.LightBlue,
        Color.LightCoral,
        Color.LightCyan,
        Color.LightGoldenrodYellow,
        Color.LightGray,
        Color.LightGreen,
        Color.LightPink,
        Color.LightSalmon,
        Color.LightSeaGreen,
        Color.LightSkyBlue,
        Color.LightSlateGray,
        Color.LightSteelBlue,
        Color.LightYellow,
        Color.Lime,
        Color.LimeGreen,
        Color.Linen,
        Color.Magenta,
        Color.Maroon,
        Color.MediumAquamarine,
        Color.MediumBlue,
        Color.MediumOrchid,
        Color.MediumPurple,
        Color.MediumSeaGreen,
        Color.MediumSlateBlue,
        Color.MediumSpringGreen,
        Color.MediumTurquoise,
        Color.MediumVioletRed,
        Color.MidnightBlue,
        Color.MintCream,
        Color.MistyRose,
        Color.Moccasin,
        Color.NavajoWhite,
        Color.Navy,
        Color.OldLace,
        Color.Olive,
        Color.OliveDrab,
        Color.Orange,
        Color.OrangeRed,
        Color.Orchid,
        Color.PaleGoldenrod,
        Color.PaleGreen,
        Color.PaleTurquoise,
        Color.PaleVioletRed,
        Color.PapayaWhip,
        Color.PeachPuff,
        Color.Peru,
        Color.Pink,
        Color.Plum,
        Color.PowderBlue,
        Color.Purple,
        Color.Red,
        Color.RosyBrown,
        Color.RoyalBlue,
        Color.SaddleBrown,
        Color.Salmon,
        Color.SandyBrown,
        Color.SeaGreen,
        Color.SeaShell,
        Color.Sienna,
        Color.Silver,
        Color.SkyBlue,
        Color.SlateBlue,
        Color.SlateGray,
        Color.Snow,
        Color.SpringGreen,
        Color.SteelBlue,
        Color.Tan,
        Color.Teal,
        Color.Thistle,
        Color.Tomato,
        Color.Turquoise,
        Color.Violet,
        Color.Wheat,
        Color.White,
        Color.WhiteSmoke,
        Color.Yellow,
        Color.YellowGreen,
        // Lua end
    };

    /// <summary>
    /// Whether the energy sword has been pulsed by a multitool,
    /// causing the blade to cycle RGB colors.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Hacked;

    /// <summary>
    ///     RGB cycle rate for hacked e-swords.
    /// </summary>
    [DataField]
    public float CycleRate = 1f;

    // Frontier: block changing colour
    /// <summary>
    ///     RGB cycle rate for hacked e-swords.
    /// </summary>
    [DataField]
    public bool BlockHacking = false;
    // End Frontier
}
