// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.StationTeleporter.Components;

[Serializable, NetSerializable]
public enum TeleporterType : byte
{
    Local,
    Sector
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationTeleporterComponent : Component
{
    [DataField("teleporterType"), AutoNetworkedField]
    public TeleporterType Type = TeleporterType.Local;

    [DataField, AutoNetworkedField]
    public string? CustomName;

    [DataField]
    public float UpdateFrequency = 1f;

    [DataField]
    public float UpdateTimer;

    [DataField]
    public SoundSpecifier LinkSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");

    [DataField]
    public SoundSpecifier UnlinkSound = new SoundPathSpecifier("/Audio/_Lua/Effects/StationTeleporter/gateway_off.ogg");

    [DataField, AutoNetworkedField]
    public EntityUid? LastLink;

    [DataField]
    public string? PortalLayerMap = "Portal";
}
