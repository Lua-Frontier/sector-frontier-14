using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Research.Discovery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ResearchComputeServerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Broken;
    [DataField, AutoNetworkedField]
    public bool ThermalOffline;
    [DataField]
    public string LinkPort = "ResearchCompute";

    [DataField, AutoNetworkedField]
    public EntityUid? LinkedMachine;

    [DataField]
    public float FailureCheckInterval = DiscoveryResearchConstants.ComputeFailureCheckSeconds;

    [DataField]
    public float HeatEnergyPerSecond = DiscoveryResearchConstants.ComputeHeatEnergyPerSecond;

    [DataField]
    public float IdlePowerLoad = DiscoveryResearchConstants.ComputeIdlePowerLoad;

    [DataField]
    public float ActivePowerLoad = DiscoveryResearchConstants.ComputeActivePowerLoad;

    [DataField, AutoNetworkedField]
    public float LoadFactor;

    [DataField]
    public float RampUpSeconds = DiscoveryResearchConstants.ComputeRampUpSeconds;

    [DataField]
    public float RampDownSeconds = DiscoveryResearchConstants.ComputeRampDownSeconds;

    [ViewVariables]
    public float FailureAccumulator;

    [ViewVariables]
    public float SparkAccumulator;

    [ViewVariables]
    public TimeSpan HeatUpdateAccumulator;
}

[Serializable, NetSerializable]
public enum ResearchComputeServerVisuals : byte
{
    Broken,
}

[Serializable, NetSerializable]
public enum ResearchComputeServerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ResearchComputeServerBoundUserInterfaceState : BoundUserInterfaceState
{
    public string Name = string.Empty;
    public bool Powered;
    public bool PowerSwitchOn;
    public bool Broken;
    public bool ThermalOffline;
    public bool UnderLoad;
    public float? TemperatureCelsius;
    public int PowerLoadWatts;
    public int HeatWatts;
    public int WorkingOnGrid;
    public int LinkedOnGrid;
    public List<ResearchComputeProcessEntry> Processes = new();
}

[Serializable, NetSerializable]
public sealed class ResearchComputeProcessEntry
{
    public int Pid;
    public string Name = string.Empty;
    public ResearchComputeProcessKind Kind;
    public ResearchComputeProcessStatus Status;
    public float Progress;
}

[Serializable, NetSerializable]
public enum ResearchComputeProcessKind : byte
{
    Scan,
    AutoScan,
    Decode,
    Print,
    Rack,
}

[Serializable, NetSerializable]
public enum ResearchComputeProcessStatus : byte
{
    Run,
    Load,
    Idle,
    Off,
    Broken,
    Thermal,
}

[Serializable, NetSerializable]
public sealed class ResearchComputeTogglePowerMessage : BoundUserInterfaceMessage;
