// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.SpaceHazards;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NebulaPresenceComponent : Component
{
    [AutoNetworkedField]
    public ProtoId<NebulaWeatherPrototype> Weather;

    [AutoNetworkedField]
    public float Intensity = 1f;

    [AutoNetworkedField]
    public List<ProtoId<NebulaWeatherPrototype>> ActiveWeathers = new();

    [AutoNetworkedField]
    public List<float> ActiveIntensities = new();
}

[RegisterComponent]
public sealed partial class NebulaThrustResistanceComponent : Component
{
    [DataField]
    public float Resistance = 1f;
}

[RegisterComponent]
public sealed partial class NebulaWeaponResistanceComponent : Component
{
    [DataField]
    public float Resistance = 1f;
}

[RegisterComponent]
public sealed partial class NebulaRadioProtectedComponent : Component;
