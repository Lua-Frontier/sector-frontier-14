using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// Shows a parallax background on the shuttle map console.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShuttleMapParallaxComponent : Component
{
    public static readonly ResPath FallbackTexture = new("/Textures/_Lua/ShuttleMap/space_background.png");

    [DataField, AutoNetworkedField]
    public ResPath TexturePath;
}
