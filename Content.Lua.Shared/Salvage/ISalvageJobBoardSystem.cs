using System.Diagnostics.CodeAnalysis;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.Salvage;

public interface ISalvageJobBoardSystem : IEntitySystem
{
    bool FulfillsSalvageJob(EntityUid uid, [NotNullWhen(true)] out ProtoId<CargoBountyPrototype>? job);
}
