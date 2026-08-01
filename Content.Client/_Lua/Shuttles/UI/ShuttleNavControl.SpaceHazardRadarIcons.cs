// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.Shuttles.Components;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared._Mono.Radar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    private void DrawSpaceHazardRadarIcons(
        DrawingHandleScreen handle,
        TransformComponent consoleXform,
        Matrix3x2 worldToShuttle,
        Matrix3x2 shuttleToView,
        Vector2 mapOrigin)
    {
        var mapId = consoleXform.MapID;
        if (mapId == MapId.Nullspace)
            return;

        var cache = IoCManager.Resolve<IResourceCache>();
        var view = worldToShuttle * shuttleToView;
        var uiXCentre = (int)Width / 2;
        var uiYCentre = (int)Height / 2;
        var scaledMousePos = GetScaledMouseUiPosition();

        const float fullScaleDistance = 512f;
        const float minDistanceScale = 0.35f;
        var maxScaleRange = MathF.Max(WorldMaxRange, fullScaleDistance + 1f);

        var celestialQuery = EntManager.AllEntityQueryEnumerator<SectorCelestialBodyComponent, RadarBlipIconComponent, TransformComponent>();
        while (celestialQuery.MoveNext(out var uid, out _, out var icon, out var xform))
            TryDrawHazardIcon(handle, cache, uid, icon, xform, mapId, mapOrigin, view, uiXCentre, uiYCentre, scaledMousePos, fullScaleDistance, minDistanceScale, maxScaleRange);

        var fieldQuery = EntManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, RadarBlipIconComponent, TransformComponent>();
        while (fieldQuery.MoveNext(out var uid, out var field, out var icon, out var xform))
        {
            if (!field.HasWeather)
                continue;

            TryDrawHazardIcon(handle, cache, uid, icon, xform, mapId, mapOrigin, view, uiXCentre, uiYCentre, scaledMousePos, fullScaleDistance, minDistanceScale, maxScaleRange);
        }
    }

    private void TryDrawHazardIcon(
        DrawingHandleScreen handle,
        IResourceCache cache,
        EntityUid uid,
        RadarBlipIconComponent icon,
        TransformComponent xform,
        MapId mapId,
        Vector2 mapOrigin,
        Matrix3x2 view,
        int uiXCentre,
        int uiYCentre,
        Vector2 scaledMousePos,
        float fullScaleDistance,
        float minDistanceScale,
        float maxScaleRange)
    {
        if (xform.MapID != mapId || icon.Icon == default)
            return;

        var worldPos = _transform.GetWorldPosition(xform);
        var worldDist = Vector2.Distance(worldPos, mapOrigin);
        if (icon.MaxDistance > 0f && worldDist > icon.MaxDistance)
            return;

        if (!cache.TryGetResource<TextureResource>(icon.Icon, out var texRes))
            return;

        var uiPosition = Vector2.Transform(worldPos, view) / UIScale;
        var uiXOffset = uiPosition.X - uiXCentre;
        var uiYOffset = uiPosition.Y - uiYCentre;
        var uiDistance = (int)Math.Sqrt(Math.Pow(uiXOffset, 2) + Math.Pow(uiYOffset, 2));
        if (uiDistance > 0)
        {
            var uiX = uiXCentre * uiXOffset / uiDistance;
            var uiY = uiYCentre * uiYOffset / uiDistance;
            var isOutsideRadarCircle = uiDistance > Math.Abs(uiX) && uiDistance > Math.Abs(uiY);
            if (isOutsideRadarCircle)
            {
                uiX = uiXCentre * uiXOffset / uiDistance * 0.95f;
                uiY = uiYCentre * uiYOffset / uiDistance * 0.95f;
                uiPosition = new Vector2(uiX + uiXCentre, uiY + uiYCentre);
            }
        }

        var isHovered = Vector2.Distance(scaledMousePos, uiPosition * UIScale) < 30f;
        var distanceScale = isHovered || worldDist <= fullScaleDistance
            ? 1f
            : MathF.Max(minDistanceScale, 1f - (worldDist - fullScaleDistance) / (maxScaleRange - fullScaleDistance) * (1f - minDistanceScale));

        var s = (RadarBlipSize * UIScale) * icon.Scale * distanceScale;
        var half = new Vector2(s / 2f, s / 2f);
        var centre = uiPosition * UIScale;

        TextureResource? secondaryTex = null;
        var hasSecondary = icon.SecondaryIcon is { } sec
                           && sec != default
                           && sec != icon.Icon
                           && cache.TryGetResource(sec, out secondaryTex);

        if (hasSecondary && secondaryTex != null)
        {
            var gap = s * 0.12f;
            var leftCentre = centre - new Vector2(half.X + gap * 0.5f, 0f);
            var rightCentre = centre + new Vector2(half.X + gap * 0.5f, 0f);
            handle.DrawTextureRect(texRes.Texture, new UIBox2(leftCentre - half, leftCentre + half));
            handle.DrawTextureRect(secondaryTex.Texture, new UIBox2(rightCentre - half, rightCentre + half));
        }
        else
        {
            handle.DrawTextureRect(texRes.Texture, new UIBox2(centre - half, centre + half));
        }

        if (icon.Label is not { } labelLoc || string.IsNullOrEmpty(labelLoc))
            return;

        var labelName = Loc.GetString(labelLoc);
        var displayedDistance = worldDist < 50f ? $"{worldDist:0.0}" : worldDist < 1000 ? $"{worldDist:0}" : $"{worldDist / 1000:0.0}k";
        var labelText = Loc.GetString("shuttle-console-iff-label", ("name", labelName), ("distance", displayedDistance));

        var textScale = UIScale * 0.9f * distanceScale;
        var labelDimensions = handle.GetDimensions(Font, labelText, 0.9f * distanceScale);
        var blipSize = RadarBlipSize * 0.7f * distanceScale;
        var labelOffset = new Vector2
        {
            X = uiPosition.X > Width / 2f
                ? -labelDimensions.X - blipSize
                : blipSize,
            Y = -labelDimensions.Y / 2f
        };

        var labelColor = Color.White;
        if (EntManager.TryGetComponent(uid, out RadarBlipComponent? blip))
            labelColor = isHovered ? blip.HighlightedRadarColor : blip.RadarColor;

        handle.DrawString(Font, (uiPosition + labelOffset) * UIScale, labelText, textScale, labelColor);
    }

    private static bool IsSpaceHazardRadarIconEntity(IEntityManager entManager, EntityUid uid)
    {
        if (entManager.HasComponent<SectorCelestialBodyComponent>(uid))
            return true;

        return entManager.TryGetComponent<AmbientSpaceFieldComponent>(uid, out var field) && field.HasWeather;
    }
}
