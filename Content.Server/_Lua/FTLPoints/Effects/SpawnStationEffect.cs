//REV
using Content.Server.GameTicking;
using Content.Server.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lua.FTLPoints.Effects;

[DataDefinition]
public sealed partial class SpawnStationEffect : FtlPointEffect
{
    [DataField("stationIds", required: true)]
    public List<string> StationIds { set; get; } = new ();

    public override void Effect(FtlPointEffectArgs args)
    {
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        var random = IoCManager.Resolve<IRobustRandom>();
        var gameTicker = args.EntityManager.System<GameTicker>();
        var gameMap = protoManager.Index<GameMapPrototype>(random.Pick(StationIds));
        gameTicker.LoadGameMap(gameMap, out MapId mapId, null);
    }
}
