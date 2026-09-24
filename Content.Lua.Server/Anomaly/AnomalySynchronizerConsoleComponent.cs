using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server.Anomaly;

[RegisterComponent]
public sealed partial class AnomalySynchronizerConsoleComponent : Component
{
    [DataField]
    public ProtoId<SourcePortPrototype> SynchronizerPort = "AnomalySynchronizerConsoleSender";

    [DataField]
    public NetEntity? Synchronizer;

    public float UiAccumulator;
}
