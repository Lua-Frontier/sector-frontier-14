//REV
using Content.Server.Weather;
using Content.Shared.Weather;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

namespace Content.Server._Lua.FTLPoints.Effects;

[DataDefinition]
public sealed partial class AddWeatherEffect : FtlPointEffect
{
    [DataField("weatherPrototypes", customTypeSerializer: typeof(PrototypeIdListSerializer<WeatherPrototype>))]
    public List<string> WeatherPrototypes { set; get; } = default!;

    public override void Effect(FtlPointEffectArgs args)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        var weatherSystem = args.EntityManager.System<WeatherSystem>();

        weatherSystem.SetWeather(args.MapId, protoManager.Index<WeatherPrototype>(random.Pick(WeatherPrototypes)), null);
        Logger.Debug("Set weather.");
    }
}
