// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Lua.SpaceHazards;

public enum NebulaWeatherKind : byte
{
    Lightning = 0,
    Corrosion = 1,
    EmpStorm = 2,
    Veil = 3,
    RadiationFog = 4,
    HeatWash = 5,
}

[Prototype]
public sealed partial class NebulaWeatherPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public NebulaWeatherKind Kind { get; private set; }

    [DataField]
    public int? PreferredColorIndex { get; private set; }

    [DataField]
    public ResPath? RadarIcon { get; private set; }

    [DataField]
    public DamageSpecifier Damage { get; private set; } = new();

    [DataField]
    public DamageSpecifier MobDamage { get; private set; } = new();

    [DataField]
    public float EmpRange { get; private set; } = 4f;

    [DataField]
    public float EmpEnergy { get; private set; } = 5000f;

    [DataField]
    public float EmpDuration { get; private set; } = 8f;

    [DataField]
    public float EmpChance { get; private set; }

    [DataField]
    public int EmpPulsesPerTick { get; private set; } = 1;

    [DataField]
    public float DamageChance { get; private set; } = 0.35f;

    [DataField]
    public int MaxDamagedPerTick { get; private set; } = 12;

    [DataField]
    public int MinEventDelaySeconds { get; private set; } = 5;

    [DataField]
    public int MaxEventDelaySeconds { get; private set; } = 10;

    [DataField]
    public int Priority { get; private set; }

    [DataField]
    public bool BlocksFtl { get; private set; }

    [DataField]
    public bool RadioBlackout { get; private set; }

    [DataField]
    public float ThrustMultiplier { get; private set; } = 1f;

    [DataField]
    public float WeaponCooldownMultiplier { get; private set; } = 1f;

    [DataField]
    public float ShieldLoad { get; private set; }

    [DataField]
    public float RadiationIntensity { get; private set; }

    [DataField]
    public float MobTemperatureIncrease { get; private set; }
}
