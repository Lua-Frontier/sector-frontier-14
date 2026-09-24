using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.Anomaly;

[RegisterComponent, NetworkedComponent]
public sealed partial class AnomalyCorePayloadComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Anomaly;
}
