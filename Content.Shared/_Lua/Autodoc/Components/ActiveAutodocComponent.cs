using Content.Shared._Shitmed.Autodoc.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Lua.Autodoc.Components;

[RegisterComponent, Access(typeof(SharedAutodocSystem))]
[AutoGenerateComponentPause]
public sealed partial class ActiveAutodocComponent : Component
{
    [DataField]
    public AutodocOperationKind Operation;

    [DataField]
    public EntityUid TargetPart;

    [DataField]
    public EntityUid? TargetOrgan;

    [DataField]
    public EntityUid? Item;

    [DataField]
    public List<EntProtoId> Surgeries = new();

    [DataField]
    public int SurgeryIndex;

    [DataField]
    public int CompletedSteps;

    [DataField]
    public int TotalSteps;

    /// <summary>
    /// Whether a step is waiting on a doafter to complete.
    /// </summary>
    [DataField]
    public bool Waiting;

    [DataField]
    public bool Failed;

    public bool SuppressSurgeryFailureEvent;

    [DataField]
    public (EntityUid, EntityUid, EntProtoId)? CurrentSurgery;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;
}
