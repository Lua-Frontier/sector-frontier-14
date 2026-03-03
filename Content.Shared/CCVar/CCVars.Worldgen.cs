using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether or not world generation is enabled.
    /// </summary>
    public static readonly CVarDef<bool> WorldgenEnabled =
        CVarDef.Create("worldgen.enabled", true, CVar.SERVERONLY); // Frontier: true

    /// <summary>
    ///     The worldgen config to use.
    /// </summary>
    public static readonly CVarDef<string> WorldgenConfig =
        CVarDef.Create("worldgen.worldgen_config", "NFDefault", CVar.SERVERONLY); // Frontier: Default<NFDefault
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
