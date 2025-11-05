// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Shared._Lua.Starmap;

[Prototype("starmapConfig")]
public sealed partial class StarmapConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public float BasePixelsPerDistance = 90f;

    [DataField]
    public float ZoomMin = 0.05f;

    [DataField]
    public float ZoomMax = 4f;

    [DataField]
    public int GridLines = 10;

    [DataField]
    public Color GridColor = Color.DarkSlateGray;

    [DataField]
    public Color BackgroundColor = new Color(5, 5, 10, 255);

    [DataField("parallaxLayers")]
    public StarmapParallaxLayer[] ParallaxLayers = Array.Empty<StarmapParallaxLayer>();

    [DataField]
    public int MinStars = 15;

    [DataField]
    public int MaxStars = 26;

    [DataField]
    public float StarDistanceMin = 5f;

    [DataField]
    public float StarDistanceMax = 15f;

    [DataField]
    public float HyperlaneMaxDistance = 1200f;

    [DataField]
    public int HyperlaneNeighbors = 3;

    [DataField("specialSectors")]
    public SpecialSectorConfig[] SpecialSectors = Array.Empty<SpecialSectorConfig>();
}

[DataDefinition]
public sealed partial class StarmapParallaxLayer
{
    [DataField]
    public float Tile = 256f;

    [DataField]
    public float Slowness = 0.30f;

    [DataField]
    public int StarsPerTile = 8;

    [DataField]
    public Color Color = new Color(255, 255, 255, 20);

    [DataField]
    public int Seed = 13;
}

[DataDefinition]
public sealed partial class SpecialSectorConfig
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public Vector2 Position = Vector2.Zero;

    [DataField]
    public Color? Color;
}


