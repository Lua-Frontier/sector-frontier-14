namespace Content.Shared._Shitmed.Medical.Surgery;

public enum StepInvalidReason
{
    None,
    MissingSkills,
    NeedsOperatingTable,
    Armor,
    SurgeryInvalid,
    MissingPreviousSteps,
    StepCompleted,
    MissingTool,
    ToolInvalid,
    DoAfterFailed,
}