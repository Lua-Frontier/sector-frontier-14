using Robust.Shared.Serialization;

namespace Content.Lua.Shared.Anomaly;

[Serializable, NetSerializable]
public enum AnomalySynchronizerConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class AnomalySynchronizerConsoleState : BoundUserInterfaceState
{
    public bool Linked;
    public NetEntity? SynchronizerEntity;
    public string SynchronizerName = string.Empty;
    public bool Powered;
    public bool HasAnomaly;
    public NetEntity? AnomalyEntity;
    public string AnomalyName = string.Empty;
    public int SeverityPercent;
    public int StabilityPercent;
    public int HealthPercent;
    public string Phase = "none";
    public bool Compressing;
    public float CompressProgress;
    public float CompressDuration = 60f;
    public bool CanCompress;
    public string ScannerText = string.Empty;
    public TimeSpan? NextPulseTime;
    public bool HasBattery;
    public int BatteryPercent;
    public bool BatteryCharging;
}

[Serializable, NetSerializable]
public sealed class AnomalySyncConnectMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class AnomalySyncDisconnectMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class AnomalySyncCompressMessage : BoundUserInterfaceMessage;
