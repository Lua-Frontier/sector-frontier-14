using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Radar;

[Serializable, NetSerializable]
public enum RadarBlipShape
{
    Circle,
    Square,
    Triangle,
    Star,
    Diamond,
    Hexagon,
    Arrow,
    Ring
}

/// <summary>
/// Networked radar blip payload. <see cref="GridConfig"/> is set when the blip is on a grid and has an override.
/// </summary>
[Serializable, NetSerializable]
public record struct RadarBlipNetData(
    NetEntity Uid,
    NetCoordinates Position,
    Vector2 Vel,
    float Scale,
    Color Color,
    RadarBlipShape Shape,
    bool SonarEcho,
    BlipConfig? GridConfig,
    Angle Rotation);

/// <summary>
/// Seeking / guided missile direction + scan-arc data for radar overlays.
/// </summary>
[Serializable, NetSerializable]
public record struct MissileVectorNetData(
    NetEntity Uid,
    float Range,
    Angle ScanArc);

[Serializable, NetSerializable]
public sealed class GiveBlipsEvent : EntityEventArgs
{
    public readonly List<RadarBlipNetData> Blips;

    /// <summary>
    /// Missile velocity / FOV arcs (Monolith seeking + SACLOS).
    /// </summary>
    public readonly List<MissileVectorNetData> Missiles;

    /// <summary>
    /// Hitscan lines to display on the radar as (start position, end position, thickness, color).
    /// </summary>
    public readonly List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> HitscanLines;

    public GiveBlipsEvent(List<RadarBlipNetData> blips)
    {
        Blips = blips;
        Missiles = new List<MissileVectorNetData>();
        HitscanLines = new List<(Vector2 Start, Vector2 End, float Thickness, Color Color)>();
    }

    public GiveBlipsEvent(
        List<RadarBlipNetData> blips,
        List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> hitscans)
    {
        Blips = blips;
        Missiles = new List<MissileVectorNetData>();
        HitscanLines = hitscans;
    }

    public GiveBlipsEvent(
        List<RadarBlipNetData> blips,
        List<MissileVectorNetData> missiles,
        List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> hitscans)
    {
        Blips = blips;
        Missiles = missiles;
        HitscanLines = hitscans;
    }
}

[Serializable, NetSerializable]
public sealed class RequestBlipsEvent : EntityEventArgs
{
    public NetEntity Radar;
    public RequestBlipsEvent(NetEntity radar)
    {
        Radar = radar;
    }
}

[Serializable, NetSerializable]
public sealed class BlipRemovalEvent : EntityEventArgs
{
    public NetEntity NetBlipUid { get; set; }

    public BlipRemovalEvent(NetEntity netBlipUid)
    {
        NetBlipUid = netBlipUid;
    }
}

/// <summary>
/// Display config for a radar blip. Used as an optional on-grid override via <see cref="RadarBlipComponent.GridConfig"/>.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial record struct BlipConfig
{
    [DataField]
    public Box2 Bounds = new Box2(-0.5f, -0.5f, 0.5f, 0.5f);

    [DataField]
    public Color Color = Color.OrangeRed;

    [DataField]
    public RadarBlipShape Shape = RadarBlipShape.Circle;

    /// <summary>
    /// When true, bounds are treated as world meters and scaled by minimap zoom on the client.
    /// </summary>
    [DataField]
    public bool RespectZoom = false;

    /// <summary>
    /// When true, the blip shape is rotated with the parent grid (client-side).
    /// </summary>
    [DataField]
    public bool Rotate = false;

    public BlipConfig() { }

    /// <summary>
    /// Approximate legacy <see cref="RadarBlipComponent.Scale"/> from bounds (Monolith backwards-compat formula).
    /// </summary>
    public float GetScale() => (Bounds.Width + Bounds.Height) / 6f;
}
