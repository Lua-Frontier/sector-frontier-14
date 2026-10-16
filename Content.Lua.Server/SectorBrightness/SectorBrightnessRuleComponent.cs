using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Lua.Server.SectorBrightness;

[RegisterComponent, Access(typeof(SectorBrightnessRule))]
public sealed partial class SectorBrightnessRuleComponent : Component
{
    [DataField]
    public string Sector = "AsteroidSectorDefault";

    [DataField]
    public SoundSpecifier? Music;

    [DataField]
    public Color AmbientColor = Color.FromHex("#5A6474");

    [DataField]
    public float MessageChancePerSecond;

    [DataField]
    public List<LocId> Messages = new();

    [ViewVariables]
    public MapId MapId = MapId.Nullspace;

    [ViewVariables]
    public EntityUid? Map;

    [ViewVariables]
    public bool AddedMapLight;

    [DataField]
    public float LightEnergyMultiplier = 1f;

    [DataField]
    public float LightSaturationMultiplier = 1f;

    /// <summary>
    /// Original energy and color of boosted lights, plus the boosted energy to detect lights changed since.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, (float Energy, Color Color, float Boosted)> BoostedLights = new();

    [ViewVariables]
    public bool Blocked;
}
