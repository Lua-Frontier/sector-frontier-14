using Robust.Shared.Utility;

namespace Content.Client._Lua.Expedition.UI;

internal static class ExpeditionPlanetSprites
{
    private const string PlanetsDir = "/Textures/_Lua/Expedition/Planets";

    private static readonly HashSet<string> KnownStates =
    [
        "Caves", "Grasslands", "Lava", "Lavalight", "LowDesert", "Shadow", "Snow",
        "GateBioluminescent", "GateCavesGrass", "GateCavesLava", "GateCavesShadow",
        "GateDesertCaves", "GateDesertGrass", "GateDesertShadow", "GateGrassCaves",
        "GateGrassDesert", "GateGrassLava", "GateGrassSnow", "GateInferno", "GateJungle",
        "GateLavaCaves", "GateLavaDesert", "GateLavaGrass", "GateLavaShadow", "GateLavaSnow",
        "GateShadowDesert", "GateShadowLava", "GateShadowSnow", "GateSnowCaves",
        "GateSnowDesert", "GateSnowLava", "GateTripleMix", "GateVolcanicSavanna", "GateWasteland",
    ];

    public static ResPath GetRsiPath(string state) => new($"{PlanetsDir}/{state}.rsi");
    public static bool TryResolve(string biomeId, out ResPath rsiPath, out string state)
    {
        if (KnownStates.Contains(biomeId))
        {
            state = biomeId;
            rsiPath = GetRsiPath(state);
            return true;
        }

        state = "Grasslands";
        rsiPath = GetRsiPath(state);
        return false;
    }
}
