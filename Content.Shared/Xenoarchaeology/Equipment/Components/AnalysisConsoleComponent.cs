using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Xenoarchaeology.Equipment.Components;

/// <summary>
/// The console that is used for artifact analysis
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AnalysisConsoleComponent : Component
{
    /// <summary>
    /// The analyzer entity the console is linked.
    /// Can be null if not linked.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? AnalyzerEntity;

    /// <summary>
    /// The machine linking port for the analyzer
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> LinkingPort = "ArtifactAnalyzerSender";
}

[ByRefEvent]
public readonly record struct ArtifactAnalyzerSampleChangedEvent(EntityUid? Sample);

[ByRefEvent]
public readonly record struct AnalysisConsoleAnalyzerLinkChangedEvent;
