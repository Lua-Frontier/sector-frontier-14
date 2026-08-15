using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Lua.NPC.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NpcSmartTurretComponent : Component
{
    [DataField, AutoNetworkedField]
    public float VisionRadius = 28f;

    [DataField, AutoNetworkedField]
    public float RangedRange = 28f;

    [DataField, AutoNetworkedField]
    public float ShootDelay = 0.05f;

    [DataField, AutoNetworkedField]
    public Angle AccuracyThreshold = Angle.FromDegrees(12);

    [DataField, AutoNetworkedField]
    public float LeadScale = 1f;

    [DataField, AutoNetworkedField]
    public float TrackMemorySeconds = 1.5f;

    [ViewVariables, AutoNetworkedField]
    public EntityCoordinates LastKnownTargetCoordinates;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan LastSeenAt;
}
