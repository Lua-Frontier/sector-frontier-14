// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
using System.Numerics;

namespace Content.Server._Lua.Shuttles.Components;

public interface IShuttleGrid
{
    bool Enabled { get; set; }
    Vector2[] CenterOfThrust { get; }
    float BaseMaxLinearVelocity { get; set; }
    float[] LinearThrust { get; }
    float[] BaseLinearThrust { get; }
    List<EntityUid>[] LinearThrusters { get; }
    List<EntityUid> AngularThrusters { get; }
    float AngularThrust { get; set; }
    DirectionFlag ThrustDirections { get; set; }
    float BodyModifier { get; set; }
    float DampingModifier { get; set; }
}

public static class ShuttleGridConstants
{
    public const float BrakeCoefficient = 1.5f;
    public const float MaxAngularVelocity = 4f;
}
