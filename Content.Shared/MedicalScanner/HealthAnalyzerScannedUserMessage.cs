using Content.Shared._Lua.MedicalScanner.UI; // Lua
using Content.Shared.FixedPoint;
using Content.Shared._Shitmed.Targeting; // Shitmed
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
///     On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    public bool? Unclonable; // Frontier
    public bool Printable; // Frontier
    public HealthAnalyzerRotTime RotTime; // Lua
    public Dictionary<TargetBodyPart, TargetIntegrity>? Body; // Shitmed
    public Dictionary<TargetBodyPart, Dictionary<string, FixedPoint2>>? BodyDamageTypes; // Shitmed
    public NetEntity? Part; // Shitmed

    public HealthAnalyzerScannedUserMessage(
        NetEntity? targetEntity,
        float temperature,
        float bloodLevel,
        bool? scanMode,
        bool? bleeding,
        bool? unrevivable,
        bool? unclonable,
        bool printable = false,
        HealthAnalyzerRotTime rotTime = HealthAnalyzerRotTime.None,
        Dictionary<TargetBodyPart, TargetIntegrity>? body = null,
        Dictionary<TargetBodyPart, Dictionary<string, FixedPoint2>>? bodyDamageTypes = null,
        NetEntity? part = null)
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        Unclonable = unclonable;
        Printable = printable;
        RotTime = rotTime;
        Body = body;
        BodyDamageTypes = bodyDamageTypes;
        Part = part;
    }
}

[Serializable, NetSerializable]
public sealed class HealthAnalyzerPartMessage(NetEntity? owner, TargetBodyPart? bodyPart) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Owner = owner;
    public readonly TargetBodyPart? BodyPart = bodyPart;
}
