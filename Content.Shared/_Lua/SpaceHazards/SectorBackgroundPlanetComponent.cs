// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._Lua.SpaceHazards;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SectorBackgroundPlanetComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SpriteRadius = 55f;

    [DataField, AutoNetworkedField]
    public float Seed;

    [DataField, AutoNetworkedField]
    public float Pixels = 1024f;

    [DataField, AutoNetworkedField]
    public PixelPlanetKind PlanetKind = PixelPlanetKind.LandRivers;

    [DataField, AutoNetworkedField]
    public byte PaletteIndex;

    [DataField, AutoNetworkedField]
    public bool VisualsInitialized;

    [DataField, AutoNetworkedField]
    public float Rotation;

    [DataField, AutoNetworkedField]
    public float LightOriginX = 0.39f;

    [DataField, AutoNetworkedField]
    public float LightOriginY = 0.39f;
}
