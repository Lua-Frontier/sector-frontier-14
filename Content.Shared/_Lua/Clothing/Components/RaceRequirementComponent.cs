using Robust.Shared.GameStates;
using Robust.Shared.Prototypes; // Для ProtoId<>
using Content.Shared.Humanoid.Prototypes; // Для SpeciesPrototype

namespace Content.Shared.Clothing.Components;

/// <summary>
///     Restrict wearing this clothing for everyone, except owner.
///     First person that equipped clothing is saved as clothing owner.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class RaceRequirementComponent : Component
{
    /// <summary>
    ///     If biocoding enabled? Can be toggled by verb.
    /// </summary>
    [DataField("enabled")]
    [AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    ///     Allowed races:
    /// </summary>
    [DataField("allowedRaces")]
    [AutoNetworkedField]
    public HashSet<ProtoId<SpeciesPrototype>> AllowedRaces = new();
}
