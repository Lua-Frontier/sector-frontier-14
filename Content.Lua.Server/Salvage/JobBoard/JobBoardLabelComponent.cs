using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server.Salvage.JobBoard;

[RegisterComponent]
public sealed partial class JobBoardLabelComponent : Component
{
    [DataField]
    public ProtoId<CargoBountyPrototype>? JobId;
}
