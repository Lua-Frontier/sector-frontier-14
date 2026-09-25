using Content.Lua.Shared.Expedition;
using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Expedition;

public interface IExpeditionSystem : IEntitySystem
{
    ExpeditionConsoleState? GetExpeditionStateForConsole(EntityUid consoleUid, EntityUid? shuttleGridUid);
}
