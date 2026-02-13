// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.
// Just f... https://www.youtube.com/watch?v=rAs2wOlIfYU

using Content.Shared._Mono.Radar;
using Robust.Client.Graphics;
using System.Numerics;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    private const float PingCycleSeconds = 2.0f;
    private const float BlipFadeFraction = 0.65f;
    private static readonly Vector2[] EllipseVerts = new Vector2[8];
    private void DrawLuaRadarBlip(DrawingHandleScreen handle, NetEntity netUid, bool sonarEcho, Vector2 position, float size, Color color, RadarBlipShape shape, ref bool handled)
    {
        if (!sonarEcho) return;
        handled = true;
        var strength = GetPingFadeStrength(position);
        if (strength <= 0.01f) return;
        var toBlip = position - MidPointVector;
        var dist = toBlip.Length();
        Vector2 dir, normal;
        if (dist > 0.5f)
        {
            dir = toBlip / dist;
            normal = new Vector2(dir.Y, -dir.X);
        }
        else
        {
            dir = new Vector2(1f, 0f);
            normal = new Vector2(0f, 1f);
        }
        var gradColor = BaroBlipGradient(strength);
        var blipScale = (strength + 3f) * size * 0.16f;
        var halfLen = MathF.Max(2f, blipScale * 1.0f);
        var halfWid = MathF.Max(1.2f, blipScale * 0.5f);
        var alpha = MathF.Min(strength, 0.9f);
        DrawSonarEllipse(handle, position, dir, normal, halfLen * 2.0f, halfWid * 2.0f, gradColor.WithAlpha(alpha * 0.06f));
        DrawSonarEllipse(handle, position, dir, normal, halfLen * 1.4f, halfWid * 1.4f, gradColor.WithAlpha(alpha * 0.15f));
        DrawSonarEllipse(handle, position, dir, normal, halfLen, halfWid, gradColor.WithAlpha(alpha * 0.38f));
        var j1 = Hash01(netUid.GetHashCode());
        var j2 = Hash01(netUid.GetHashCode() * 7919);
        var ghostOfs = dir * (j1 * blipScale) + normal * ((j2 - 0.5f) * blipScale * 2f);
        var ghostR = blipScale * 0.5f;
        handle.DrawCircle(position + ghostOfs, ghostR * 1.5f, gradColor.WithAlpha(alpha * 0.5f * 0.08f), filled: true);
        handle.DrawCircle(position + ghostOfs, ghostR, gradColor.WithAlpha(alpha * 0.5f * 0.2f), filled: true);
    }
    private float GetPingFadeStrength(Vector2 position)
    {
        var displayRadius = MathF.Max(Size.X, Size.Y) * 0.5f;
        var blipDist = Vector2.Distance(position, MidPointVector);
        var normalizedDist = MathF.Min(blipDist / MathF.Max(displayRadius, 1f), 1f);
        var phase = (float)(Timing.CurTime.TotalSeconds % PingCycleSeconds) / PingCycleSeconds;
        var timeSinceSwept = phase - normalizedDist;
        if (timeSinceSwept < 0f) timeSinceSwept += 1f;
        if (timeSinceSwept > BlipFadeFraction) return 0f;
        var strength = 1f - timeSinceSwept / BlipFadeFraction;
        return MathF.Pow(strength, 0.7f);
    }

    private static Color BaroBlipGradient(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        if (t < 0.30f) return Color.InterpolateBetween(new Color(0, 160, 180), new Color(0, 210, 210), t / 0.30f);
        if (t < 0.60f) return Color.InterpolateBetween(new Color(0, 210, 210), new Color(2, 230, 160), (t - 0.30f) / 0.30f);
        if (t < 0.80f) return Color.InterpolateBetween(new Color(2, 230, 160), new Color(2, 240, 100), (t - 0.60f) / 0.20f);
        return Color.InterpolateBetween(new Color(2, 240, 100), Color.White, (t - 0.80f) / 0.20f);
    }

    private static void DrawSonarEllipse(DrawingHandleScreen handle, Vector2 center, Vector2 dir, Vector2 normal, float halfLen, float halfWid, Color color)
    {
        for (var i = 0; i < 8; i++)
        {
            var a = i * MathF.Tau / 8f;
            EllipseVerts[i] = center + dir * (MathF.Cos(a) * halfLen) + normal * (MathF.Sin(a) * halfWid);
        }
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, EllipseVerts, color);
    }

    private static float Hash01(int x)
    {
        unchecked
        {
            x ^= (x << 13);
            x ^= (x >> 17);
            x ^= (x << 5);
        }
        return (x & 0x7FFFFFFF) / (float) int.MaxValue;
    }
}
