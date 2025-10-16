// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Shipyard;

[Serializable, NetSerializable]
public enum SimpleShipVoucherUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SimpleShipVoucherInterfaceState : BoundUserInterfaceState
{
    public List<string> VesselIds { get; }
    public List<string> VesselNames { get; }

    public SimpleShipVoucherInterfaceState(List<string> vesselIds, List<string> vesselNames)
    {
        VesselIds = vesselIds;
        VesselNames = vesselNames;
    }
}

