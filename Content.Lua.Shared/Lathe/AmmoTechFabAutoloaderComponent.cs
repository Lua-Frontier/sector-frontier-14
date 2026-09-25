// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Lua.Shared.Lathe;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class AmmoTechFabAutoloaderComponent : Component
{
    public const string ContainerId = "ammo_techfab_autoloader";

    [DataField]
    public string Container = ContainerId;

    [DataField]
    public float RoundsPerSecond = 5f;

    [DataField]
    public float MaxRoundsPerSecond = 30f;

    [DataField]
    public SoundSpecifier? SoundLoad = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/bullet_insert.ogg");

    [DataField]
    public TimeSpan SoundInterval = TimeSpan.FromSeconds(0.45);

    [DataField, AutoNetworkedField]
    public bool Starved;

    [ViewVariables]
    public float Budget;

    [ViewVariables]
    public int Cursor;

    [ViewVariables]
    public TimeSpan NextSound;

    [ViewVariables]
    public float ChoiceRefresh = 1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class AmmoTechFabAutoloadComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Cartridge;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> Choices = new();

    [DataField, AutoNetworkedField]
    public string Group = string.Empty;

    [DataField, AutoNetworkedField]
    public bool Selected;

    [DataField, AutoNetworkedField]
    public bool CanLoad;

    [DataField, AutoNetworkedField]
    public bool WaitingMaterials;

    public float Accumulator;
}
