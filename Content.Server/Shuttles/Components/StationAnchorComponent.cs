using Content.Server.Shuttles.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.DeviceLinking; // Frontier

namespace Content.Server.Shuttles.Components;

[RegisterComponent]
[Access(typeof(StationAnchorSystem))]
public sealed partial class StationAnchorComponent : Component
{
    // Frontier: Add ports for linking
    [DataField("onPort")]
    public ProtoId<SinkPortPrototype> OnPort = "On";

    [DataField("offPort")]
    public ProtoId<SinkPortPrototype> OffPort = "Off";

    [DataField("togglePort")]
    public ProtoId<SinkPortPrototype> TogglePort = "Toggle";
    // End Frontier

    [DataField("switchedOn")]
    public bool SwitchedOn { get; set; } = true;
}
