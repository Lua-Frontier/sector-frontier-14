using System.Numerics;
using Content.Shared.DeviceLinking;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Research.Discovery;

namespace Content.Lua.Shared.Research.Discovery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiscoverySourceConsoleComponent : Component
{
    [DataField]
    public string DiskSlotId = "research_disk_slot";

    [DataField, AutoNetworkedField]
    public bool Scanning;
    [DataField, AutoNetworkedField]
    public bool AutoScanning;

    [DataField, AutoNetworkedField]
    public float ScanProgress;

    [DataField, AutoNetworkedField]
    public float ScanDuration = DiscoveryResearchConstants.BaseScanSeconds;

    [DataField, AutoNetworkedField]
    public Vector2 SignalNormalized = new(0.5f, 0.5f);
    [DataField, AutoNetworkedField]
    public Vector2 AimNormalized = new(0.5f, 0.5f);
    public Vector2 AimInput;
    [DataField, AutoNetworkedField]
    public string? SelectedNodeId;

    [DataField, AutoNetworkedField]
    public bool SignalLocked;

    [DataField]
    public string ComputePort = "ResearchCompute";
    [DataField, AutoNetworkedField]
    public EntityUid? SourceEntity;
    [DataField, AutoNetworkedField]
    public NetEntity? VesselEntity;

    [DataField]
    public ProtoId<SourcePortPrototype> VesselLinkingPort = "DiscoveryAnomalyVesselSender";
}
[RegisterComponent, NetworkedComponent]
public sealed partial class DiscoveryConsoleOperatorComponent : Component
{
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiscoveryAnalysisConsoleComponent : Component
{
    [DataField]
    public string DiskSlotId = "research_disk_slot";

    [DataField, AutoNetworkedField]
    public bool Decoding;

    [DataField]
    public string ComputePort = "ResearchCompute";
}
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiscoveryJournalConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? PendingBlueprintRecipe;

    [DataField, AutoNetworkedField]
    public bool Printing;

    [DataField, AutoNetworkedField]
    public float PrintProgress;

    [DataField, AutoNetworkedField]
    public float PrintDuration = (float)DiscoveryResearchConstants.BlueprintPrintTime.TotalSeconds;
}

[Serializable, NetSerializable]
public enum DiscoverySourceUiKey : byte { Key }

[Serializable, NetSerializable]
public enum DiscoveryAnalysisUiKey : byte { Key }

[Serializable, NetSerializable]
public enum DiscoveryJournalUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class DiscoverySourceBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool HasDisk;
    public ResearchDataDiskStage DiskStage;
    public bool HasAnalyzer;
    public bool ArtifactPadMode;
    public bool HasSource;
    public bool HasServer;
    public int UniqueDataPercent = -1;
    public string? SourceName;
    public NetEntity? SourceEntity;
    public bool Scanning;
    public bool AutoScanning;
    public float ScanProgress;
    public float ScanDuration;
    public Vector2 AimNormalized;
    public Vector2 SignalNormalized;
    public bool SignalLocked;
    public int LinkedComputeCount;
    public int WorkingComputeCount;
    public int ServerPoints;
    public int UnlockedCount;
    public string? PreviewDiscipline;
    public int PreviewTierHint;
    public List<string> PreviewTags = new();
    public int PreviewCostEstimate;
    public string PreviewRisk = string.Empty;
    public string? SelectedNodeId;
    public List<DiscoveryArtifactNodeEntry> ArtifactNodes = new();
}

[Serializable, NetSerializable]
public sealed class DiscoveryArtifactNodeEntry
{
    public string Id = string.Empty;
    public int Depth;
    public bool Locked;
    public bool Active;
    public bool CanUnlock;
    public bool Selected;
    public int Durability;
    public int MaxDurability;
    public int ResearchValue;
    public string Effect = string.Empty;
}

[Serializable, NetSerializable]
public sealed class DiscoveryAnalysisBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool HasDisk;
    public ResearchDataDiskStage DiskStage;
    public bool HasServer;
    public int ServerPoints;
    public bool Decoding;
    public float DecodeProgress;
    public float DecodeCost;
    public float DecodeDuration;
    public string? TechId;
    public string? TechName;
    public string? Discipline;
    public int Cost;
    public List<string> RecipeIds = new();
    public bool AlreadyUnlocked;
    public int LinkedComputeCount;
    public int WorkingComputeCount;
    public string? PreviewDiscipline;
    public int PreviewTierHint;
    public List<string> PreviewTags = new();
    public int PreviewCostEstimate;
    public string PreviewRisk = string.Empty;
}

[Serializable, NetSerializable]
public sealed class DiscoveryJournalBoundUserInterfaceState : BoundUserInterfaceState
{
    public int Points;
    public string? Faction;
    public bool HasServer;
    public List<DiscoveryUnlockedEntry> Unlocked = new();
    public int WorkingComputeHint;
    public bool Printing;
    public float PrintProgress;
    public float PrintDuration;
}

[Serializable, NetSerializable]
public sealed class DiscoveryUnlockedEntry
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Discipline = string.Empty;
    public string? DisciplineName;
    public Color DisciplineColor;
    public string? EntityIcon;
    public int Cost;
    public int Tier;
    public List<string> RecipeIds = new();
    public List<string> RecipeNames = new();
    public bool IsSignature;
}

[Serializable, NetSerializable]
public sealed class DiscoverySelectNodeMessage : BoundUserInterfaceMessage
{
    public string NodeId = string.Empty;
    public DiscoverySelectNodeMessage(string nodeId) => NodeId = nodeId;
}

[Serializable, NetSerializable]
public sealed class DiscoveryTuneSignalMessage : BoundUserInterfaceMessage
{
    public Vector2 Direction;
    public DiscoveryTuneSignalMessage(Vector2 direction) => Direction = direction;
}

[Serializable, NetSerializable]
public sealed class DiscoveryStartScanMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiscoverySetAutoMessage : BoundUserInterfaceMessage
{
    public bool Enabled;
    public DiscoverySetAutoMessage(bool enabled) => Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class DiscoveryEjectDiskMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiscoveryStartDecodeMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiscoveryUploadMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiscoveryClearDiskMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiscoveryConvertDuplicateMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DiscoveryPrintBlueprintMessage : BoundUserInterfaceMessage
{
    public string RecipeId = string.Empty;
    public DiscoveryPrintBlueprintMessage(string recipeId) => RecipeId = recipeId;
}
