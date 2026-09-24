using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Shared.Research.Prototypes;

/// <summary>
/// This is a prototype for a technology that can be unlocked.
/// </summary>
[Prototype]
public sealed partial class TechnologyPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The name of the technology.
    /// Supports locale strings
    /// </summary>
    [DataField(required: true)]
    public LocId Name = string.Empty;

    /// <summary>
    /// An icon used to visually represent the technology in UI.
    /// Frontier: If not specified and EntityIcon is provided, will use the entity's sprite automatically.
    /// </summary>
    [DataField] // Frontier: Not required
    public SpriteSpecifier? Icon = null; // Frontier: Not required

    /// <summary>
    /// Frontier: An entity prototype whose sprite will be used as the technology icon.
    /// If specified, this takes precedence over Icon when Icon is not provided.
    /// </summary>
    [DataField]
    public EntProtoId? EntityIcon = null;

    /// <summary>
    /// What research discipline this technology belongs to.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TechDisciplinePrototype> Discipline;

    [DataField]
    public int Tier = 1;

    /// <summary>
    /// Hidden tech is not ever available at the research console.
    /// </summary>
    [DataField]
    public bool Hidden;

    [DataField]
    public List<ProtoId<RndFactionPrototype>> Factions = new();

    [DataField]
    public Dictionary<ProtoId<RndFactionPrototype>, TechnologyFactionOverride> FactionOverrides = new();

    /// <summary>
    /// How much research is needed to unlock.
    /// </summary>
    [DataField]
    public int Cost = 10000;

    [DataField]
    public bool InstantUnlock = false;

    /// <summary>
    /// A list of <see cref="TechnologyPrototype"/>s that need to be unlocked in order to unlock this technology.
    /// </summary>
    [DataField]
    public List<ProtoId<TechnologyPrototype>> TechnologyPrerequisites = new();

    /// <summary>
    /// A list of <see cref="LatheRecipePrototype"/>s that are unlocked by this technology
    /// </summary>
    [DataField]
    public List<ProtoId<LatheRecipePrototype>> RecipeUnlocks = new();

    /// <summary>
    /// A list of non-standard effects that are done when this technology is unlocked.
    /// </summary>
    [DataField]
    public IReadOnlyList<GenericUnlock> GenericUnlocks = new List<GenericUnlock>();

    [DataField]
    public List<string> Tags = new();

    [DataField]
    public ProtoId<RndFactionPrototype>? SignatureFor;

    [DataField]
    public float DiscoveryWeight = 1f;

    /// Frontier: R&D console rework
    /// <summary>
    /// Position of this tech in console menu
    /// </summary>
    [DataField]
    public Vector2? Position { get; private set; }

    /// <summary>
    /// Defines the visual style of prerequisite connection lines leading TO this technology.
    /// This controls how the lines from prerequisite techs to this tech are drawn.
    /// </summary>
    [DataField]
    public PrerequisiteLineType PrerequisiteLineType { get; private set; } = PrerequisiteLineType.LShape;

    /// <summary>
    /// Get all disciplines this technology belongs to.
    /// </summary>
    public List<ProtoId<TechDisciplinePrototype>> GetAllDisciplines()
    {
        return new List<ProtoId<TechDisciplinePrototype>> { Discipline };
    }

    /// <summary>
    /// Check if this technology belongs to a specific discipline.
    /// </summary>
    public bool HasDiscipline(ProtoId<TechDisciplinePrototype> disciplineId)
    {
        return Discipline == disciplineId;
    }
    /// End Frontier: R&D console rework
}

[DataDefinition]
public sealed partial class TechnologyFactionOverride
{
    [DataField]
    public Vector2? Position;

    [DataField]
    public List<ProtoId<TechnologyPrototype>>? TechnologyPrerequisites;
}

[DataDefinition]
public partial record struct GenericUnlock()
{
    /// <summary>
    /// What event is raised when this is unlocked?
    /// Used for doing non-standard logic.
    /// </summary>
    [DataField]
    public object? PurchaseEvent = null;

    /// <summary>
    /// A player facing tooltip for what the unlock does.
    /// Supports locale strings.
    /// </summary>
    [DataField]
    public string UnlockDescription = string.Empty;
}

// Frontier: This is used to define how the prerequisite lines are drawn in the R&D console UI.
/// <summary>
/// Defines the visual style of prerequisite connection lines
/// </summary>
public enum PrerequisiteLineType : byte
{
    /// <summary>
    /// Clean L-shaped connections (default)
    /// </summary>
    LShape = 0,

    /// <summary>
    /// Direct diagonal lines
    /// </summary>
    Diagonal = 1,

    /// <summary>
    /// Tree-like branching connections with structured hierarchy
    /// </summary>
    Tree = 2,

    /// <summary>
    /// Spread connections that avoid overlaps by using offset routing paths
    /// </summary>
    Spread = 3
}
// End Frontier
