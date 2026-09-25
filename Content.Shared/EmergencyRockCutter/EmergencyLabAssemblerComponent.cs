using Content.Shared.Research.Prototypes;
using Content.Shared.RCD;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.EmergencyRockCutter;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmergencyLabAssemblerComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<RndFactionPrototype>? BoundFaction;
    [DataField, AutoNetworkedField]
    public bool KitInitialized;
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<RCDPrototype>> RemainingPrototypes = new();
    [DataField]
    public List<ProtoId<RCDPrototype>> SharedPrototypes = new()
    {
        "EmergencyLabCircuitImprinter",
        "EmergencyLabAutolathe",
        "EmergencyLabProtolathe",
        "EmergencyLabAnalysisConsole",
        "EmergencyLabArtifactAnalyzer",
    };
    [DataField]
    public Dictionary<ProtoId<RndFactionPrototype>, List<ProtoId<RCDPrototype>>> FactionPrototypes = new()
    {
        ["Nanotrasen"] = new() { "EmergencyLabRndConsoleNanotrasen", "EmergencyLabRndServerNanotrasen" },
        ["Syndicate"] = new() { "EmergencyLabRndConsoleSyndicate", "EmergencyLabRndServerSyndicate" },
        ["Pirates"] = new() { "EmergencyLabRndConsolePirates", "EmergencyLabRndServerPirates" },
        ["Ussp"] = new() { "EmergencyLabRndConsoleUssp", "EmergencyLabRndServerUssp" },
        ["Security"] = new() { "EmergencyLabRndConsoleSecurity", "EmergencyLabRndServerSecurity" },
        ["Neutral"] = new() { "EmergencyLabRndConsoleNeutral", "EmergencyLabRndServerNeutral" },
        ["LuaTech"] = new() { "EmergencyLabRndConsoleLuaTech", "EmergencyLabRndServerLuaTech" },
    };
    [DataField]
    public ProtoId<RndFactionPrototype> DefaultFaction = "Neutral";
}
