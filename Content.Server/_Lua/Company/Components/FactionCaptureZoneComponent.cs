using Robust.Shared.GameStates;

namespace Content.Server._Lua.Company.Components;

[RegisterComponent]
public sealed partial class FactionCaptureZoneComponent : Component
{
    [DataField]
    public float? CaptureRadius;

    [DataField]
    public int? RequiredAttackers;

    [DataField]
    public float? CaptureDuration;

    [DataField]
    public bool? ResetOnDefenderPresence;

    [DataField]
    public bool? PausedIfNoAttackers;
}