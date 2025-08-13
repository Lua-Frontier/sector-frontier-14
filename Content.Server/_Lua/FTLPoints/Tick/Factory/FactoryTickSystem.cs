//REV
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Lua.FTLPoints.Tick.Factory;

/// <summary>
/// This system spawns ships every tick.
/// </summary>
public sealed class FactoryTickSystem : StarmapTickSystem<FactoryTickComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    protected override void Ticked(EntityUid uid, FactoryTickComponent component, float frameTime)
    {
        base.Ticked(uid, component, frameTime);

        var transform = Transform(uid);
        var selectedMap = _random.Pick(component.MapPaths); // Выбираем случайную карту

        if (_mapLoader.TryLoadMapWithId(transform.MapID, selectedMap, out var mapEntity, out var grids, new DeserializationOptions()))
        {
            Log.Debug($"Successfully created a new ship using map: {selectedMap}");
        }
        else
        {
            Log.Warning($"Failed to create ship. Could not load map: {selectedMap}");
        }
    }
}
