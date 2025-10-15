// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared._Lua.SelfDestruct;
using Robust.Shared.Audio;

namespace Content.Server._Lua.SelfDestruct;

[RegisterComponent]
public sealed partial class SelfDestructComponent : Component
{
    [DataField]
    public int TimerSeconds = 300;

    [DataField]
    public int PinLength = 6;

    [DataField]
    public bool PinSet;

    [DataField]
    public string Pin = string.Empty;

    [DataField]
    public float Remaining;

    [DataField]
    public bool CountingDown;

    [DataField]
    public SelfDestructStatus Status = SelfDestructStatus.Locked;

    [DataField]
    public string? ShuttleId;

    [DataField]
    public string AnnouncementSenderLoc = "self-destruct-announcement-sender";

    [DataField]
    public float AlertSoundTime = 30.0f;

    [DataField]
    public bool PlayedAlertSound;

    [DataField]
    public bool PlayedFinalAlarm;

    [DataField]
    public string ExplosionType = "HardBombShipGun";

    [DataField]
    public int TotalIntensity = 3000000;

    [DataField]
    public int IntensitySlope = 100;

    [DataField]
    public int MaxIntensity = 1000;

    [DataField]
    public SoundSpecifier ArmSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Machines/Nuke/nuke_alarm.ogg");

    [DataField]
    public SoundSpecifier LocalLoopAlarm = new SoundPathSpecifier("/Audio/_Lua/Alarm/sirenlua.ogg");

    [DataField]
    public float LocalLoopRange = 35f;
}


