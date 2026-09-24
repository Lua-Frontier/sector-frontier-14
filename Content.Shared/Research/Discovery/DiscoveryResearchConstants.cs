using Content.Shared.Atmos;

namespace Content.Shared.Research.Discovery;

public static class DiscoveryResearchConstants
{
    public const float DuplicatePointsFraction = 0.8f;
    public const int MinTechYield = 1;
    public const int MaxTechYield = 6;
    public const int TechYieldWeightHalving = 2;
    public const float TechDiscoveryChance = 0.12f;
    public const float ArtifactNodeUnlockChance = 0.08f;
    public const float BaseScanSeconds = 45f;
    public const float DecodeSecondsPerThousandCost = 12f;
    public const float SecondsPerUnlockedTech = 2.5f;
    public const float FirstComputeSpeedFactor = 0.55f;
    public const float AdditionalComputeDiminishFactor = 0.92f;
    public const float NoComputePenalty = 2.75f;
    public const float AutoScanNoComputeMultiplier = 1.5f;
    public const int ScanPointsPerSecond = 15;
    public static readonly TimeSpan BlueprintPrintTime = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ComputeMeanTimeBetweenFailures = TimeSpan.FromHours(12);
    public static readonly TimeSpan ComputeSparkInterval = TimeSpan.FromSeconds(2.5);
    public const float ComputeFailureCheckSeconds = 300f;
    public const float ComputeHeatEnergyPerSecond = 7500f;
    public const float ComputeIdlePowerLoad = 150f;
    public const float ComputeActivePowerLoad = 4500f;
    public const float ComputeRampUpSeconds = 10f;
    public const float ComputeRampDownSeconds = 4f;
    public const float ComputeExternalBreakDamage = 32f;
    public const float ComputeBreakCascadeRange = 2.5f;
    public const string ComputeBreakExplosionType = "Default";
    public const float ComputeBreakExplosionIntensity = 8f;
    public const float ComputeBreakExplosionSlope = 4f;
    public const float ComputeBreakExplosionMaxTile = 1.5f;
    public static readonly TimeSpan ComputeHeatUpdateSpacing = TimeSpan.FromSeconds(1);
    public const float ComputeMaxOperatingTemperature = Atmospherics.T0C + 100f;
    public const float ComputeMinOperatingTemperature = Atmospherics.T0C - 60f;
    public const float SignalTuneSpeed = 0.55f;
    public const float SignalLockRadius = 0.14f;
    public const float SignalMissResistance = 0.35f;
    public const string ResearchDataDiskPrototype = "ResearchDataDisk";
    public const string PaperMaterial = "Paper";
}
