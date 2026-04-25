using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Blocking.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShieldToggleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleShield";

    [AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    [AutoNetworkedField]
    public EntityUid? Wearer;

    [DataField]
    public SoundSpecifier? SoundActivate;

    [DataField]
    public SoundSpecifier? SoundDeactivate;

    [DataField]
    public SoundSpecifier? SoundFailToActivate;

    [DataField]
    public SoundSpecifier? ActiveSound;

    public EntityUid? PlayingStream;
}

public sealed partial class ToggleShieldEvent : InstantActionEvent;

[ByRefEvent]
public record struct ShieldToggleAttemptEvent(EntityUid? User, bool Cancelled = false);
