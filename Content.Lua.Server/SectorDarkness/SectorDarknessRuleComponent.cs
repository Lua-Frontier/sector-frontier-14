using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Lua.Server.SectorDarkness;

[RegisterComponent, Access(typeof(SectorDarknessRule))]
public sealed partial class SectorDarknessRuleComponent : Component
{
    [DataField]
    public string Sector = "AsteroidSectorDefault";

    [DataField]
    public SoundSpecifier? Music;

    [DataField]
    public float LightBreakChancePerSecond;

    [DataField]
    public float DoorToggleChancePerSecond;

    [DataField]
    public float LightFlickerChancePerSecond;

    [DataField]
    public float MessageChancePerSecond;

    [DataField]
    public List<LocId> Messages = new();

    [ViewVariables]
    public MapId MapId = MapId.Nullspace;

    [ViewVariables]
    public EntityUid? Map;

    [ViewVariables]
    public bool Blocked;
}
