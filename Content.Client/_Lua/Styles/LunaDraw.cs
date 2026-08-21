// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.Styles;

public static class LunaDraw
{
    public const float HairlineHalfWidth = 0.5f;
    private static readonly ProtoId<ShaderPrototype> LineShaderId = "LunaSdfLine";
    private static readonly ProtoId<ShaderPrototype> DiskShaderId = "LunaSdfDisk";
    private static ShaderInstance? _lineShader;
    private static ShaderInstance? _diskShader;
    private static readonly DrawVertexUV2DColor[] QuadVerts = new DrawVertexUV2DColor[4];
    private static ShaderInstance LineShader => _lineShader ??= IoCManager.Resolve<IPrototypeManager>().Index(LineShaderId).InstanceUnique();
    private static ShaderInstance DiskShader => _diskShader ??= IoCManager.Resolve<IPrototypeManager>().Index(DiskShaderId).InstanceUnique();

    public static void Line(DrawingHandleScreen handle, Vector2 from, Vector2 to, Color color, float thickness = 1f)
    {
        if (color.A <= 0f) return;
        var delta = to - from;
        var length = delta.Length();
        if (length < 0.001f) return;
        var half = MathF.Max(0.35f, thickness * 0.5f);
        var pad = half + 1.5f;
        var dir = delta / length;
        var n = new Vector2(-dir.Y, dir.X);
        var a = from - dir * pad;
        var b = to + dir * pad;
        var segLen = length;
        var paramsUv = new Vector2(half, segLen);
        var stroke = color;
        QuadVerts[0] = Vert(a - n * pad, paramsUv, new Vector2(-pad, -pad), stroke);
        QuadVerts[1] = Vert(a + n * pad, paramsUv, new Vector2(-pad, pad), stroke);
        QuadVerts[2] = Vert(b - n * pad, paramsUv, new Vector2(segLen + pad, -pad), stroke);
        QuadVerts[3] = Vert(b + n * pad, paramsUv, new Vector2(segLen + pad, pad), stroke);
        handle.UseShader(LineShader);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, QuadVerts);
        handle.UseShader(null);
    }
    public static void Disk(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        if (radius <= 0.01f || color.A <= 0f)
            return;

        DrawDiskInternal(handle, center, radius, halfWidth: 0f, color);
    }

    public static void Ring(DrawingHandleScreen handle, Vector2 center, float radius, Color color, float thickness = 1f)
    {
        if (radius <= 0.01f || color.A <= 0f) return;
        DrawDiskInternal(handle, center, radius, MathF.Max(0.35f, thickness * 0.5f), color);
    }

    public static void Circle(DrawingHandleScreen handle, Vector2 center, float radius, Color color, bool filled = true)
    {
        if (filled) Disk(handle, center, radius, color);
        else Ring(handle, center, radius, color);
    }

    public static void Polyline(DrawingHandleScreen handle, Vector2[] points, Color color, float thickness = 1f, bool closed = true)
    {
        if (points == null) return;
        Polyline(handle, points, points.Length, color, thickness, closed);
    }

    public static void Polyline(DrawingHandleScreen handle, Vector2[] points, int count, Color color, float thickness = 1f, bool closed = true)
    {
        if (points == null || count < 2 || color.A <= 0f) return;
        count = Math.Min(count, points.Length);
        var last = closed ? count : count - 1;
        for (var i = 0; i < last; i++) Line(handle, points[i], points[(i + 1) % count], color, thickness);
    }

    public static void DashedLine(DrawingHandleScreen handle, Vector2 from, Vector2 to, Color color, float dashLength = 10f, float gapLength = 6f, float thickness = 1f, float offset = 0f)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length < 0.01f || color.A <= 0f) return;
        dashLength = MathF.Max(2f, dashLength);
        gapLength = MathF.Max(1f, gapLength);
        var period = dashLength + gapLength;
        var dir = delta / length;
        var cursor = 0f;
        if (offset != 0f && period > 0f)
        {
            var o = offset % period;
            if (o < 0f)
                o += period;
            if (o < dashLength)
            {
                Line(handle, from, from + dir * MathF.Min(length, dashLength - o), color, thickness);
                cursor = dashLength - o + gapLength;
            }
            else
            {
                cursor = period - o;
            }
        }

        var draw = true;
        while (cursor < length)
        {
            var step = draw ? dashLength : gapLength;
            var next = MathF.Min(length, cursor + step);
            if (draw && next > cursor)
                Line(handle, from + dir * cursor, from + dir * next, color, thickness);
            cursor = next;
            draw = !draw;
        }
    }

    public static void DashedPolyline(DrawingHandleScreen handle, Vector2[] points, Color color, float dashLength, float gapLength, float thickness = 1f, bool closed = true)
    {
        if (points == null) return;
        DashedPolyline(handle, points, points.Length, color, dashLength, gapLength, thickness, closed);
    }

    public static void DashedPolyline(DrawingHandleScreen handle, Vector2[] points, int count, Color color, float dashLength, float gapLength, float thickness = 1f, bool closed = true)
    {
        if (points == null || count < 2) return;
        count = Math.Min(count, points.Length);
        var last = closed ? count : count - 1;
        for (var i = 0; i < last; i++) DashedLine(handle, points[i], points[(i + 1) % count], color, dashLength, gapLength, thickness);
    }

    private static void DrawDiskInternal(DrawingHandleScreen handle, Vector2 center, float radius, float halfWidth, Color color)
    {
        var pad = (halfWidth < 0.01f ? radius : radius + halfWidth) + 1.5f;
        var paramsUv = new Vector2(radius, halfWidth);
        var stroke = color;
        QuadVerts[0] = Vert(center + new Vector2(-pad, -pad), paramsUv, new Vector2(-pad, -pad), stroke);
        QuadVerts[1] = Vert(center + new Vector2(pad, -pad), paramsUv, new Vector2(pad, -pad), stroke);
        QuadVerts[2] = Vert(center + new Vector2(-pad, pad), paramsUv, new Vector2(-pad, pad), stroke);
        QuadVerts[3] = Vert(center + new Vector2(pad, pad), paramsUv, new Vector2(pad, pad), stroke);
        handle.UseShader(DiskShader);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, QuadVerts);
        handle.UseShader(null);
    }

    private static DrawVertexUV2DColor Vert(Vector2 pos, Vector2 paramsUv, Vector2 uv2, Color stroke)
    {
        var v = new DrawVertexUV2DColor(pos, paramsUv, stroke);
        v.UV2 = uv2;
        return v;
    }
}
