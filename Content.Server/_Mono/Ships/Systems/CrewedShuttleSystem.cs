using Content.Server.Shuttles.Components;
using Content.Shared._Mono.FireControl;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Content.Shared.Silicons.StationAi; // Lua

namespace Content.Server._Mono.Ships.Systems;

/// <summary>
/// This handles ensuring a crewed shuttle is only piloted and gunned by two separate people.
/// </summary>
public sealed class CrewedShuttleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public bool AnyConsoleActiveOnGridByPlayer<T>(EntityUid grid, Enum key, EntityUid actor) where T : IComponent
    {
        if (HasComp<Shared._Mono.Ships.Components.WhitelistConsolesComponent>(grid)) return false; // Lua

        if (HasComp<StationAiHeldComponent>(actor)) return false; // Lua

        var query = EntityQueryEnumerator<T>();

        while (query.MoveNext(out var uid, out _))
        {
            var xform = Transform(uid);
            var consoleGrid = xform.GridUid;

            if (consoleGrid != grid && xform.ParentUid != grid)
                continue;

            if (!TryComp<UserInterfaceComponent>(uid, out var ui))
                continue;

            var result = _ui.IsUiOpen((uid, ui), key, actor);

            if (result)
                return true;
        }

        return false;
    }

    public bool AnyGunneryConsoleActiveOnGridByPlayer(EntityUid grid, EntityUid actor)
    {
        return AnyConsoleActiveOnGridByPlayer<FireControlConsoleComponent>(grid, FireControlConsoleUiKey.Key, actor);
    }

    public bool AnyShuttleConsoleActiveOnGridByPlayer(EntityUid grid, EntityUid actor)
    {
        return AnyConsoleActiveOnGridByPlayer<ShuttleConsoleComponent>(grid, ShuttleConsoleUiKey.Key, actor);
    }
}
