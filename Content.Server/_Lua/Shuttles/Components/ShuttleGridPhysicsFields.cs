// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Shuttles.Components;

internal static class ShuttleGridPhysicsFields
{
    public static void CopyFrom(IShuttleGrid target, IShuttleGrid source)
    {
        target.Enabled = source.Enabled;
        target.BaseMaxLinearVelocity = source.BaseMaxLinearVelocity;
        target.AngularThrust = source.AngularThrust;
        target.ThrustDirections = source.ThrustDirections;
        target.BodyModifier = source.BodyModifier;
        target.DampingModifier = source.DampingModifier;
        for (var i = 0; i < 4; i++)
            target.CenterOfThrust[i] = source.CenterOfThrust[i];
    }
}
