using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Company;

public interface IFactionWarSystem : IEntitySystem
{
    bool AreFactionSectorsUnlocked();
}
