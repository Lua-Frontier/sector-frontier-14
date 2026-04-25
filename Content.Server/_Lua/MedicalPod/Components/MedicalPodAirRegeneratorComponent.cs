namespace Content.Server._Lua.MedicalPod.Components;

[RegisterComponent]
public sealed partial class MedicalPodAirRegeneratorComponent : Component
{
    [DataField]
    public float TargetOxygenMoles = 40f;

    [DataField]
    public float TargetNitrogenMoles = 100f;

    [DataField]
    public float TargetWaterVaporMoles = 3f;

    [DataField]
    public float MaxCarbonDioxideMoles = 0f;

    [DataField]
    public float UpdateInterval = 0f;

    [DataField]
    public TimeSpan NextUpdate;
}
