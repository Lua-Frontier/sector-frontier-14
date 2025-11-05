// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Starmap;

[NetSerializable, Serializable]
public enum SectorCaptureUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SectorCaptureBuiState : BoundUserInterfaceState
{
    public bool IsCapturing;
    public float ProgressBar;
    public string Faction;
    public bool CanCapture;

    public SectorCaptureBuiState(bool isCapturing, float progressbar, string faction, bool canCapture)
    {
        IsCapturing = isCapturing;
        ProgressBar = progressbar;
        Faction = faction;
        CanCapture = canCapture;
    }
}

[Serializable, NetSerializable]
public sealed class StartSectorCaptureBuiMsg : BoundUserInterfaceMessage
{ }


