using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether or not world generation is enabled.
    /// </summary>
    public static readonly CVarDef<bool> WorldgenEnabled =
        CVarDef.Create("worldgen.enabled", false, CVar.SERVERONLY); // Lua: false all generation in asteroid sector

    /// <summary>
    ///     The worldgen config to use.
    /// </summary>
    public static readonly CVarDef<string> WorldgenConfig =
        CVarDef.Create("worldgen.worldgen_config", "NFDefault", CVar.SERVERONLY); // Frontier: Default<NFDefault

    public static readonly CVarDef<bool> WorldgenDebrisPregenEnabled =
        CVarDef.Create("worldgen.debris_pregen_enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<float> WorldgenDebrisPregenRadius =
        CVarDef.Create("worldgen.debris_pregen_radius", 30000f, CVar.SERVERONLY);

    public static readonly CVarDef<bool> WorldgenDebrisClusterEnabled =
        CVarDef.Create("worldgen.debris_cluster_enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<float> WorldgenDebrisClusterSpacing =
        CVarDef.Create("worldgen.debris_cluster_spacing", 768f, CVar.SERVERONLY);

    public static readonly CVarDef<float> WorldgenDebrisClusterRadius =
        CVarDef.Create("worldgen.debris_cluster_radius", 250f, CVar.SERVERONLY);

    public static readonly CVarDef<float> WorldgenDebrisClusterJitter =
        CVarDef.Create("worldgen.debris_cluster_jitter", 0.28f, CVar.SERVERONLY);

    public static readonly CVarDef<float> WorldgenDebrisClusterCountScale =
        CVarDef.Create("worldgen.debris_cluster_count_scale", 1.3f, CVar.SERVERONLY);

    public static readonly CVarDef<float> BiomeLoadRange =
        CVarDef.Create("biome.load_range", 16f, CVar.ARCHIVE | CVar.SERVERONLY);
    public static readonly CVarDef<int> BiomeChunkBudget =
        CVarDef.Create("biome.chunk_budget", 3, CVar.ARCHIVE | CVar.SERVERONLY);
    public static readonly CVarDef<int> BiomeMarkerBudget =
        CVarDef.Create("biome.marker_budget", 20, CVar.ARCHIVE | CVar.SERVERONLY);
    public static readonly CVarDef<int> BiomeMarkerChunkBudget =
        CVarDef.Create("biome.marker_chunk_budget", 2, CVar.ARCHIVE | CVar.SERVERONLY);
    public static readonly CVarDef<int> BiomeDecalBudget =
        CVarDef.Create("biome.decal_budget", 21, CVar.ARCHIVE | CVar.SERVERONLY);
    public static readonly CVarDef<int> BiomeEntityBudget =
        CVarDef.Create("biome.entity_budget", 21, CVar.ARCHIVE | CVar.SERVERONLY);
}
