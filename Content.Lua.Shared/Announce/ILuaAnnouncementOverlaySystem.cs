using Content.Lua.Shared.Announce;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Lua.Shared.Announce;

public interface ILuaAnnouncementOverlaySystem : IEntitySystem
{
    void Dispatch(Filter filter, AnnouncementOverlayParams overlay);
}
