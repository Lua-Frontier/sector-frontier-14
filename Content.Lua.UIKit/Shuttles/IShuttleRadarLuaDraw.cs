// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Mono.Radar;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Lua.UIKit.Shuttles;
public interface IShuttleRadarLuaDraw
{
    void DrawNavOverlays(in ShuttleNavLuaDrawContext ctx);
    void DrawNavRadarBlip(in ShuttleNavLuaBlipContext ctx, ref bool handled);
    bool IsRadarBlipIconDrawnElsewhere(IEntityManager entManager, EntityUid uid, bool showIff);
    void DrawMapOverlays(in ShuttleMapLuaDrawContext ctx);
    void DrawMapDroneRoutes(in ShuttleMapLuaDroneContext ctx);
}

public readonly struct ShuttleNavLuaDrawContext
{
    public required DrawingHandleScreen Handle { get; init; }
    public required IEntityManager EntManager { get; init; }
    public required SharedTransformSystem Transform { get; init; }
    public required TransformComponent ConsoleXform { get; init; }
    public required Matrix3x2 WorldToShuttle { get; init; }
    public required Matrix3x2 ShuttleToView { get; init; }
    public required Vector2 ConsoleMapPos { get; init; }
    public required float WorldRange { get; init; }
    public required float Width { get; init; }
    public required float Height { get; init; }
    public required float UIScale { get; init; }
    public required Font Font { get; init; }
    public required Vector2 ScaledMouseUiPos { get; init; }
    public required int RadarBlipSize { get; init; }
    public IReadOnlyList<DroneRouteState>? DroneRoutes { get; init; }
    public HashSet<NetEntity>? DroneRouteFilter { get; init; }
}

public readonly struct ShuttleNavLuaBlipContext
{
    public required DrawingHandleScreen Handle { get; init; }
    public required NetEntity NetUid { get; init; }
    public required bool SonarEcho { get; init; }
    public required Vector2 Position { get; init; }
    public required float Size { get; init; }
    public required Color Color { get; init; }
    public required RadarBlipShape Shape { get; init; }
    public required Vector2 MidPoint { get; init; }
    public required Vector2 ControlSize { get; init; }
    public required TimeSpan CurTime { get; init; }
}

public readonly struct ShuttleMapLuaDrawContext
{
    public required DrawingHandleScreen Handle { get; init; }
    public required IEntityManager EntManager { get; init; }
    public required SharedTransformSystem Transform { get; init; }
    public required MapId ViewingMap { get; init; }
    public required Matrix3x2 MapTransform { get; init; }
    public required Box2 ViewBox { get; init; }
    public required Vector2 EyeOffset { get; init; }
    public required float MinimapScale { get; init; }
    public required float UIScale { get; init; }
    public required Font Font { get; init; }
    public required Func<Vector2, Vector2> ScalePosition { get; init; }
    public required Action<DrawingHandleScreen, string, Vector2, float, Color, Color> DrawSoftText { get; init; }
}

public readonly struct ShuttleMapLuaDroneContext
{
    public required DrawingHandleScreen Handle { get; init; }
    public required IEntityManager EntManager { get; init; }
    public required SharedTransformSystem Transform { get; init; }
    public required MapId ViewingMap { get; init; }
    public required Matrix3x2 MapTransform { get; init; }
    public required float AnimOffset { get; init; }
    public required Func<Vector2, Vector2> ScalePosition { get; init; }
    public IReadOnlyList<DroneRouteState>? DroneRoutes { get; init; }
}
