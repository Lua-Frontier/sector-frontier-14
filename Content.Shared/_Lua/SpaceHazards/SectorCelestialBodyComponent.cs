// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Lua.SpaceHazards;

public enum CelestialKind : byte
{
    Star = 0,
    BlackHole = 1,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SectorCelestialBodyComponent : Component
{
    [DataField, AutoNetworkedField]
    public CelestialKind Kind = CelestialKind.Star;

    [DataField, AutoNetworkedField]
    public float SpriteRadius = 48f;

    [DataField, AutoNetworkedField]
    public float Seed;

    [DataField, AutoNetworkedField]
    public float Pixels = 1024f;

    [DataField, AutoNetworkedField]
    public byte Palette;

    [DataField, AutoNetworkedField]
    public bool VisualsInitialized;

    [DataField, AutoNetworkedField]
    public float HazardRadius = 400f;

    [DataField, AutoNetworkedField]
    public float RadiationRange = 600f;

    [DataField, AutoNetworkedField]
    public float PullRadius = 900f;

    [DataField, AutoNetworkedField]
    public float EventHorizonRadius = 120f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier StarDamage = new()
    {
        DamageDict = { ["Structural"] = 500, ["Blunt"] = 200, ["Heat"] = 150 },
    };

    [DataField, AutoNetworkedField]
    public DamageSpecifier HorizonDamage = new()
    {
        DamageDict = { ["Structural"] = 800, ["Blunt"] = 300, ["Heat"] = 200 },
    };

    [DataField, AutoNetworkedField]
    public DamageSpecifier MobHazardDamage = new()
    {
        DamageDict = { ["Heat"] = 10 },
    };

    [DataField, AutoNetworkedField]
    public DamageSpecifier MobRadiationDamage = new()
    {
        DamageDict = { ["Radiation"] = 6 },
    };

    [DataField, AutoNetworkedField]
    public float PullAcceleration = 18f;
}
