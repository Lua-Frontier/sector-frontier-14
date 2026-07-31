// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Lua.AmbientSpaceEffects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AmbientSpaceFieldComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<AmbientSpaceEffectPrototype>)), AutoNetworkedField]
    public string Effect = "Nebula";

    [DataField, AutoNetworkedField]
    public float Radius = 250f;

    [DataField, AutoNetworkedField]
    public int Seed;

    [DataField, AutoNetworkedField]
    public float Density = 0.55f;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#5AD0FF").WithAlpha(0.85f);

    [DataField, AutoNetworkedField]
    public ProtoId<NebulaWeatherPrototype>? Weather;
}
