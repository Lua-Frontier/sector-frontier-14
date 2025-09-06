using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Implants.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TeleportOnCritImplantComponent : Component
{
    /// <summary>
    /// Куда телепортировать владельца (координаты или EntityUid точки).
    /// </summary>
    [DataField("teleportTarget")]
    public EntityUid? TeleportTarget;

    /// <summary>
    /// Прототип органа, который будет создан на месте тела.
    /// </summary>
    [DataField("organPrototype")]
    public EntProtoId? OrganPrototype;

    /// <summary>
    /// Сколько органов создать.
    /// </summary>
    [DataField("organCount")]
    public int OrganCount = 3;
}
