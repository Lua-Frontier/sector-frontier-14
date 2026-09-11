namespace Content.Server._Mono.Planets;

[RegisterComponent]
public sealed partial class PlanetMapComponent : Component
{
    [DataField]
    public string Parallax = "bedrock";
}
