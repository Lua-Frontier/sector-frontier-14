// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Backmen.Disease;

namespace Content.Shared._Lua.Disease.Components;

[Serializable, NetSerializable]
public enum DiseaseAnalyzerStatus : byte
{
    NotAnalyzed,
    Analyzing,
    Analyzed
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DiseaseAnalyzerComponent : Component
{
    [ViewVariables]
    public DiseaseAnalyzerStatus Status = DiseaseAnalyzerStatus.NotAnalyzed;

    [ViewVariables]
    public ProtoId<DiseasePrototype>[]? DiseaseIDs;

    [DataField("sampleSlot")]
    public string SampleSlotID = "sample_slot";

    [DataField]
    public EntProtoId ReportPrototype = "DiagnosisReportPaper";

    [DataField]
    public EntProtoId DiseaseContainerPrototype = "SampleTube";

    // Timers

    [ViewVariables]
    public float AnalyzingTimer;

    [ViewVariables]
    public float ReportReloadTimer;

    [DataField]
    public float AnalyzingTime = 10f; // Seconds

    [DataField]
    public float ReportReloadTime = 10f; // Seconds

    // Sounds

    [DataField]
    public SoundSpecifier AnalyzingSound = new SoundPathSpecifier("/Audio/Machines/scan_loop.ogg")
    {
        Params = AudioParams.Default.WithLoop(true).WithMaxDistance(3)
    };

    [ViewVariables]
    public EntityUid? AnalyzingSoundStream;

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/diagnoser_printing.ogg");

    [DataField]
    public SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier FinishSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");

    [DataField]
    public SoundSpecifier ClearSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");
}
