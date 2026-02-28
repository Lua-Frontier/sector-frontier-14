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
        CVarDef.Create("biome.load_range", 11f, CVar.ARCHIVE | CVar.SERVERONLY);
    public static readonly CVarDef<int> BiomeChunkBudget =
        CVarDef.Create("biome.chunk_budget", 3, CVar.ARCHIVE | CVar.SERVERONLY);
}
