//REV
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Serilog;

namespace Content.Server._Lua.FTLPoints.Effects;

[DataDefinition]
public sealed partial class SpawnMapEffect : FtlPointEffect
{
    [DataField("mapPaths", required: true)]
    public List<ResPath> MapPaths { set; get; } = new List<ResPath>()
    {
        new ResPath("/Maps/_FTL/trade-station.yml")
    };

    public override void Effect(FtlPointEffectArgs args)
    {
        var mapLoader = args.EntityManager.System<MapLoaderSystem>();
        var random = IoCManager.Resolve<IRobustRandom>();
        var selectedMap = random.Pick(MapPaths);

        if (mapLoader.TryLoadMapWithId(args.MapId, selectedMap, out var mapEntity, out var grids, new DeserializationOptions()))
        {
            Log.Debug($"Successfully loaded map: {selectedMap}");
        }
        else
        {
            Log.Warning($"Failed to load map: {selectedMap}");
        }
    }
}
