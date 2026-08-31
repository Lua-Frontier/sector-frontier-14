// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using System.Numerics;

namespace Content.Server._Lua.Shuttles.Components;

[RegisterComponent]
public sealed partial class WrecksGridComponent : Component, IShuttleGrid
{
    [ViewVariables]
    public bool Enabled = true;

    [ViewVariables]
    public Vector2[] CenterOfThrust { get; } = new Vector2[4];

    [ViewVariables(VVAccess.ReadWrite)]
    public float BaseMaxLinearVelocity = 50f;

    [ViewVariables]
    public float[] LinearThrust { get; } = new float[4];

    [ViewVariables]
    public float[] BaseLinearThrust { get; } = new float[4];

    public List<EntityUid>[] LinearThrusters { get; } =
    [
        new(),
        new(),
        new(),
        new(),
    ];

    public List<EntityUid> AngularThrusters { get; } = new();

    [ViewVariables]
    public float AngularThrust;

    [ViewVariables]
    public DirectionFlag ThrustDirections = DirectionFlag.None;

    [DataField]
    public float BodyModifier = 0.45f;

    [DataField]
    public float DampingModifier;

    bool IShuttleGrid.Enabled { get => Enabled; set => Enabled = value; }
    float IShuttleGrid.BaseMaxLinearVelocity { get => BaseMaxLinearVelocity; set => BaseMaxLinearVelocity = value; }
    float IShuttleGrid.AngularThrust { get => AngularThrust; set => AngularThrust = value; }
    DirectionFlag IShuttleGrid.ThrustDirections { get => ThrustDirections; set => ThrustDirections = value; }
    float IShuttleGrid.BodyModifier { get => BodyModifier; set => BodyModifier = value; }
    float IShuttleGrid.DampingModifier { get => DampingModifier; set => DampingModifier = value; }
}
