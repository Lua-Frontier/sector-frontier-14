// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Salvage.Expeditions.Modifiers;
using Content.Shared.Weather;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Stargate.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SoftPlanetOverlayComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WeatherOpacity = 0.45f;

    [DataField, AutoNetworkedField]
    public float GasOverlayOpacity = 0.15f;

    public static readonly HashSet<ProtoId<WeatherPrototype>> DenseWeather = new()
    {
        "Ashfall",
        "AshfallLight",
        "AshfallHeavy",
        "Fallout",
        "Hail",
        "Sandstorm",
        "SandstormHeavy",
        "SnowfallMedium",
        "SnowfallHeavy",
        "Storm",
    };

    public static readonly HashSet<ProtoId<SalvageAirMod>> DenseAirMods = new()
    {
        "GateAirHumid",
        "GateAirToxic",
        "GateAirSwamp",
        "GateAirPlasmaTrace",
        "GateAirHeavyToxic",
        "GateAirPlasma",
        "GateAirVolatile",
        "GateAirFrezon",
        "GateAirAmmonia",
        "Mix3",
        "Mix4",
        "Mix6",
        "Mix7",
        "Mix8",
        "Mix9",
        "Mix11",
        "Mix12",
        "Mix13",
        "Mix16",
        "Mix17",
        "Mix18",
        "Unknown1",
        "Unknown2",
        "Unknown3",
        "Unknown4",
        "Unknown5",
    };

    public static bool IsDenseWeather(ProtoId<WeatherPrototype> weatherId)
        => DenseWeather.Contains(weatherId);

    public static bool IsDenseAirMod(ProtoId<SalvageAirMod> airModId)
        => DenseAirMods.Contains(airModId);

    public float GetWeatherOpacity(ProtoId<WeatherPrototype> weatherId)
        => IsDenseWeather(weatherId) ? WeatherOpacity : 1f;
}
