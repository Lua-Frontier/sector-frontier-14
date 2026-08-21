// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Administration.AlertLevel;

[Serializable, NetSerializable]
public sealed class AlertLevelAdminSectorInfo
{
    public string SectorId = string.Empty;
    public string SectorName = string.Empty;
    public string CurrentLevel = string.Empty;
    public bool Locked;
    public string LevelColorHex = "#FFFFFF";
}

[Serializable, NetSerializable]
public sealed class AlertLevelAdminLevelInfo
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string ColorHex = "#FFFFFF";
    public bool Selectable = true;
}

[Serializable, NetSerializable]
public sealed class AlertLevelAdminEuiState : EuiStateBase
{
    public List<AlertLevelAdminSectorInfo> Sectors = new();
    public List<AlertLevelAdminLevelInfo> Levels = new();
    public string StatusText = string.Empty;
}

public static class AlertLevelAdminEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class RefreshRequest : EuiMessageBase;

    [Serializable, NetSerializable]
    public sealed class SetSectorRequest : EuiMessageBase
    {
        public string SectorId = string.Empty;
        public string Level = string.Empty;
        public bool Locked;
    }

    [Serializable, NetSerializable]
    public sealed class SetGlobalRequest : EuiMessageBase
    {
        public string Level = string.Empty;
        public bool Locked;
    }
}
