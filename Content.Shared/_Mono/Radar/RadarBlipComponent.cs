using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Radar;

[RegisterComponent, NetworkedComponent]
public sealed partial class RadarBlipComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("radarColor")]
    public Color RadarColor = Color.Red;

    [ViewVariables(VVAccess.ReadWrite), DataField("highlightedRadarColor")]
    public Color HighlightedRadarColor = Color.OrangeRed;

    [DataField]
    public float Scale = 1;

    [DataField]
    public RadarBlipShape Shape = RadarBlipShape.Circle;

    [DataField]
    public bool RequireNoGrid = false;

    [DataField]
    public bool VisibleFromOtherGrids = true;

    [DataField]
    public bool Enabled = true;

    [DataField]
    public float MaxDistance = 1024f;

    /// <summary>
    /// If set, used instead of <see cref="RadarColor"/>/<see cref="Scale"/>/<see cref="Shape"/>
    /// while the blip is parented to a grid (ship footprint markers, etc).
    /// </summary>
    [DataField]
    public BlipConfig? GridConfig;

    // Lua: Добавлена булевая переменная на просчёт координат для блипа из компонента физики в случае true или из кэша с обновлением каждые 60 секунд если false. (bool variable determining whether blip coordinates are calculated from the physics component (if true) or from the cache with a 60-second update interval (if false).
    // Нужна для отрисовки блипов на не-физических статичных объектах, таких как маркеры взрывов, области, и т.д. (It is needed for rendering blips on non-physical static objects, such as explosion markers, areas, etc.)
    [ViewVariables(VVAccess.ReadWrite), DataField("physics")]
    public bool Physics = true;
}
