using Content.Shared._Shitmed.Autodoc.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Autodoc.Components;

/// <summary>
/// God component for autodoc.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedAutodocSystem))]
[AutoGenerateComponentState]
public sealed partial class AutodocComponent : Component
{
    public const string DefaultBodyContainerId = "autodoc-bodyContainer";

    /// <summary>
    /// Container slot holding the patient being operated on.
    /// </summary>
    [DataField]
    public string BodyContainerId = DefaultBodyContainerId;

    [ViewVariables]
    public ContainerSlot? BodyContainer;

    [DataField, AutoNetworkedField]
    public List<AutodocProgram> Programs = new();

    /// <summary>
    /// The hand to hold surgery-specific items in (organs etc).
    /// After an operation this gets put back into storage.
    /// </summary>
    [DataField]
    public string ItemSlot = "surgery_specific";

    /// <summary>
    /// How long to wait between processing program steps while active.
    /// </summary>
    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// The maximum number of programs this autodoc can have.
    /// </summary>
    [DataField]
    public int MaxPrograms = 16;

    /// <summary>
    /// How long a program title is allowed to be.
    /// </summary>
    public int MaxProgramTitleLength = 20;

    /// <summary>
    /// The maximum number of steps a program can have.
    /// </summary>
    [DataField]
    public int MaxProgramSteps = 16;
}

[Serializable, NetSerializable]
public enum AutodocVisuals : byte
{
    Status
}

[Serializable, NetSerializable]
public enum AutodocStatus : byte
{
    Off,
    Open,
    Occupied
}

[Serializable, NetSerializable]
public enum AutodocWireStatus : byte
{
    PowerIndicator
}
