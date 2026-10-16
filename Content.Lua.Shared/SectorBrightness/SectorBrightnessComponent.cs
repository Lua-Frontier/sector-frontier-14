using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Lua.Shared.SectorBrightness;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SectorBrightnessComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier? Music;

    /// <summary>
    /// Ambient light of the map at full strength. Grids keep their own lighting thanks to implicit roofs.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color AmbientColor = Color.FromHex("#5A6474");
}
