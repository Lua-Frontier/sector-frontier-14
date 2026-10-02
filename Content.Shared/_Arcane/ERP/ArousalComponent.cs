using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Arcane.ERP;

public enum ArousalPhase : byte
{
    Calm,
    Interested,
    Aroused,
    Heated,
    Peak,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause]
public sealed partial class ArousalComponent : Component
{

    [AutoNetworkedField]
    public float LastValue;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan LastChangeTime;

    [DataField, AutoNetworkedField]
    public float DecayRate = 0.3f;

    [DataField]
    public float MaxArousal = 100f;

    public Dictionary<string, float> PassiveSources = [];

    public float PassiveGainRate
    {
        get
        {
            var total = 0f;
            foreach (var rate in PassiveSources.Values)
                total += rate;
            return total;
        }
    }

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextPhaseCheckAt;

    [AutoNetworkedField]
    public ArousalPhase CurrentPhase;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan LastOrgasmAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan RefractoryUntil;

    [DataField]
    public TimeSpan RefractoryDuration = TimeSpan.FromSeconds(60);

    public ArousalPhase ComputePhase(float arousal) => arousal switch
    {
        < 20f => ArousalPhase.Calm,
        < 40f => ArousalPhase.Interested,
        < 70f => ArousalPhase.Aroused,
        < 100f => ArousalPhase.Heated,
        _ => ArousalPhase.Peak,
    };
}
