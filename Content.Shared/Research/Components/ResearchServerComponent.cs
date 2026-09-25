using Robust.Shared.GameStates;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Research.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ResearchServerComponent : Component
{
    [DataField("faction"), ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<RndFactionPrototype> Faction = "Nanotrasen";

    [DataField("serverType")]
    public string LegacyServerType
    {
        get => Faction;
        set => Faction = value;
    }

    /// <summary>
    /// The name of the server
    /// </summary>
    [AutoNetworkedField]
    [DataField("serverName"), ViewVariables(VVAccess.ReadWrite)]
    public string ServerName = "RDSERVER";

    /// <summary>
    /// The amount of points on the server.
    /// </summary>
    [AutoNetworkedField]
    [DataField("points"), ViewVariables(VVAccess.ReadWrite)]
    public int Points;

    /// <summary>
    /// A unique numeric id representing the server
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public int Id;

    /// <summary>
    /// Entities connected to the server
    /// </summary>
    /// <remarks>
    /// This is not safe to read clientside
    /// </remarks>
    [ViewVariables(VVAccess.ReadOnly)]
    public List<EntityUid> Clients = new();

    [DataField("nextUpdateTime", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextUpdateTime = TimeSpan.Zero;

    [DataField("researchConsoleUpdateTime"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan ResearchConsoleUpdateTime = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxActiveSlots = 4;

    [DataField, AutoNetworkedField]
    public List<ResearchFocusTarget> FocusTargets = new();

    [DataField, AutoNetworkedField]
    public List<ResearchProjectEntry> ActiveProjects = new();

    [DataField, AutoNetworkedField]
    public List<ResearchProjectEntry> QueuedProjects = new();

    [DataField, AutoNetworkedField]
    public Dictionary<string, int> TechnologyProgress = new();
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class ResearchFocusTarget
{
    [DataField]
    public string Technology = string.Empty;

    [DataField]
    public List<string> PathSteps = new();
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class ResearchProjectEntry
{
    [DataField]
    public string Technology = string.Empty;

    [DataField]
    public string TargetId = string.Empty;

    [DataField]
    public int Progress;

    [DataField]
    public int Cost;

    [DataField]
    public bool Started;

    [DataField]
    public TimeSpan ResearchEndTime;
}

/// <summary>
/// Event raised on a server's clients when the point value of the server is changed.
/// </summary>
/// <param name="Server"></param>
/// <param name="Total"></param>
/// <param name="Delta"></param>
[ByRefEvent]
public readonly record struct ResearchServerPointsChangedEvent(EntityUid Server, int Total, int Delta);

/// <summary>
/// Event raised every second to calculate the amount of points added to the server.
/// </summary>
/// <param name="Server"></param>
/// <param name="Points"></param>
[ByRefEvent]
public record struct ResearchServerGetPointsPerSecondEvent(EntityUid Server, int Points);

