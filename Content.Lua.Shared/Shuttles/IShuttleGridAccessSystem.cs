// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Lua.Shared.Shuttles;

public enum ShuttleGridKind : byte
{
    Shuttle,
    Station,
    Event,
    ShuttleAi,
    Debris,
    Wrecks,
}

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

public static class ShuttleGridKinds
{
    public static bool TryParse(string value, out ShuttleGridKind kind)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "shuttle":
                kind = ShuttleGridKind.Shuttle;
                return true;
            case "station":
                kind = ShuttleGridKind.Station;
                return true;
            case "event":
                kind = ShuttleGridKind.Event;
                return true;
            case "shuttleai":
                kind = ShuttleGridKind.ShuttleAi;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}

public interface IShuttleGridAccessSystem : IEntitySystem
{
    ShuttleGridKind? GetKind(EntityUid uid);
    bool IsPilotableGrid(EntityUid uid);
    bool IsMobileShuttle(EntityUid uid);
    bool HasFtlGrid(EntityUid uid);
    bool HasAnyGridType(EntityUid uid);
    bool TryGetShuttleGrid(EntityUid uid, [NotNullWhen(true)] out IShuttleGrid? grid);
    void EnsureGridType(EntityUid uid, ShuttleGridKind kind, IShuttleGrid? copyFrom = null);
    void InitializeGrid(EntityUid uid);
    ShuttleGridKind ResolveGridType(EntityUid uid);
    bool IsDebrisKind(ShuttleGridKind kind);
}

public delegate void ShuttleGridEventHandler<in TEvent>(EntityUid uid, IShuttleGrid grid, TEvent args) where TEvent : notnull;

public delegate void ShuttleGridRefEventHandler<TEvent>(EntityUid uid, IShuttleGrid grid, ref TEvent args) where TEvent : struct;
