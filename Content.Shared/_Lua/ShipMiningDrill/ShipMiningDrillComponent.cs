// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Lua.ShipMiningDrill;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipMiningDrillComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public float MineInterval = 0.4f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextMine;

    [DataField]
    public List<Vector2> MiningOffsets = new()
    {
        new(-1f, -2f),
        new(0f, -2f),
        new(1f, -2f),
        new(2f, -2f),
        new(3f, -2f),
    };

    [DataField]
    public List<Vector2> MountOffsets = new()
    {
        new(0f, 0f),
        new(1f, 0f),
        new(2f, 0f),
        new(0f, -1f),
        new(1f, -1f),
        new(2f, -1f),
    };

    [DataField]
    public Vector2 DisposalOffset = new(1f, 0f);

    [DataField]
    public float PickupRange = 1.25f;

    [DataField]
    public int MaxPickupPerTick = 12;

    [DataField]
    public List<ProtoId<TagPrototype>> PickupTags = new()
    {
        "Ore",
        "Gems",
        "ArtifactFragment",
    };

    [DataField]
    public DamageSpecifier EntityDamage = new();

    [DataField]
    public float IdlePowerLoad = 1500f;

    [DataField]
    public float ActivePowerLoad = 4000f;
}
