// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Starmap;

[Serializable, NetSerializable]
public enum ShareSectorUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public readonly record struct ShareSectorEntry(string SectorId, string NameLocKey, string X, string Y, string[] CompanyIds);

[Serializable, NetSerializable]
public sealed class ShareSectorBoundUserInterfaceState(List<ShareSectorEntry> sectors) : BoundUserInterfaceState
{
    public readonly List<ShareSectorEntry> Sectors = sectors;
}

[Serializable, NetSerializable]
public sealed class ShareSectorSelectedMessage(string sectorId) : BoundUserInterfaceMessage
{
    public readonly string SectorId = sectorId;
}
