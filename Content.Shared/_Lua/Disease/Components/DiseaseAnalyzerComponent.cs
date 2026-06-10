// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared._Lua.Disease.Components;

public enum DiseaseAnalyzerStatus
{
    NotAnalyzed,
    Analyzing,
    Analyzed
}

[Serializable, NetSerializable]
public enum DiseaseAnalyzerVisuals
{
    IsOn,
    IsPrinting
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseAnalyzerComponent : Component
{
    // Dynamic

    [DataField, AutoNetworkedField]
    public DiseaseAnalyzerStatus Status = DiseaseAnalyzerStatus.NotAnalyzed;

    [DataField, AutoNetworkedField]
    public string[]? DiseaseIDs;

    [DataField, AutoNetworkedField]
    public ItemSlot SampleContainerSlot = new()
    {
        Whitelist = new()
        {
            Components = ["DiseaseContainer"]
        }
    };

    [DataField, AutoNetworkedField]
    public bool Powered = false;

    [DataField, AutoNetworkedField]
    public TimeSpan AnalyzingStartTime;

    [DataField, AutoNetworkedField]
    public TimeSpan ReportReloadStartTime;

    // Static

    [DataField]
    public int AnalyzingTime = 10; // Seconds

    [DataField]
    public int ReportReloadTime = 10; // Seconds

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ReportPrototype = "DiagnosisReportPaper";

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string DiseaseContainerPrototype = "SampleTube";

    // Sounds

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/diagnoser_printing.ogg");

    [DataField]
    public SoundSpecifier AnalyzingSound = new SoundPathSpecifier("/Audio/Machines/scan_loop.ogg");

    [DataField]
    public SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier FinishSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");

    [DataField]
    public SoundSpecifier ClearSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    // Sprites

    [DataField]
    public string? IdleState = null;
}
