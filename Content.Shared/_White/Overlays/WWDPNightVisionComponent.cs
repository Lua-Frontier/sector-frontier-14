using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._White.Overlays;

[RegisterComponent, NetworkedComponent]
public sealed partial class WWDPNightVisionComponent : SwitchableVisionOverlayComponent
{
    public override EntProtoId? ToggleAction { get; set; } = "WWDPToggleNightVision";

    public override Color Color { get; set; } = Color.FromHex("#98FB98");
}

public sealed partial class WWDPToggleNightVisionEvent : InstantActionEvent;
