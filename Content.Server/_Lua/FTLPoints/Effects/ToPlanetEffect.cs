using System.Linq;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Parallax.Biomes;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Utility;
using Serilog;

namespace Content.Server._Lua.FTLPoints.Effects;

[DataDefinition]
public sealed partial class ToPlanetEffect : FtlPointEffect
{
    [DataField("lightingColors")]
    public List<string> LightingColors { set; get; } = new List<string>()
    {
        "D8B059",
        "E6CB8B",
        "2b3143",
        "A34931"
    };

    [DataField("biomeTemplates", customTypeSerializer: typeof(PrototypeIdListSerializer<BiomeTemplatePrototype>))]
    public List<string> BiomeTemplates { set; get; } = default!;

    public override void Effect(FtlPointEffectArgs args)
    {
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        var random = IoCManager.Resolve<IRobustRandom>();
        var biomeTemplate = protoManager.Index<BiomeTemplatePrototype>(random.Pick(BiomeTemplates));

        var biome = args.EntityManager.EnsureComponent<BiomeComponent>(args.MapUid);
        var biomeSystem = args.EntityManager.System<BiomeSystem>();
        MetaDataComponent? metadata = null;

        biomeSystem.SetSeed(args.MapUid, biome, random.Next());
        biomeSystem.SetTemplate(args.MapUid, biome, biomeTemplate);
        args.EntityManager.Dirty(args.MapUid, biome);

        var gravity = args.EntityManager.EnsureComponent<GravityComponent>(args.MapUid);
        gravity.Enabled = true;
        args.EntityManager.Dirty(args.MapUid, gravity, metadata);

        var light = args.EntityManager.EnsureComponent<MapLightComponent>(args.MapUid);
        light.AmbientLightColor = Color.FromHex("#" + random.Pick(LightingColors));
        args.EntityManager.Dirty(args.MapUid, light, metadata);

        // Atmos
        var atmos = args.EntityManager.EnsureComponent<MapAtmosphereComponent>(args.MapUid);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;

        var mixture = new GasMixture(2500)
        {
            Temperature = 293.15f,
            // Moles = moles,
        };

        // args.EntityManager.System<AtmosphereSystem>().SetMapAtmosphere(args.MapUid, false, mixture, atmos);

        args.EntityManager.EnsureComponent<MapGridComponent>(args.MapUid);
    }
}
