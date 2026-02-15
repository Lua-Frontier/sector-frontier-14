using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Lua.Rotting;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FoodRottingComponent : Component
{
    public const int MaxStages = 3;
    public const int StageCount = 1 + MaxStages;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<TimeSpan> StageDurations = new()
    {
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(7),
        TimeSpan.FromMinutes(7),
        TimeSpan.FromMinutes(6)
    };

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public TimeSpan Accumulator = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public int Stage;

    [DataField, AutoNetworkedField]
    public bool ForceProgression;

    [DataField]
    public Color BaseColor = Color.White;

    [DataField]
    public List<Color> StageColors = new();

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<ReagentPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string PuddleReagent = "Mold";

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 PuddleAmount = FixedPoint2.New(2);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool PuddleSound = true;
}

