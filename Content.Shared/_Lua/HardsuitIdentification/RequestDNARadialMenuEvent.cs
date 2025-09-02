// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// Special Permission:
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project
// the right to use this code under a separate custom license agreement.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.HardsuitIdentification;

[Serializable, NetSerializable]
public sealed partial class RequestDNARadialMenuEvent : EntityEventArgs
{
    public NetEntity Target;
    public List<string> AvailableActionPrototypes;

    public RequestDNARadialMenuEvent(NetEntity target, List<string> availableActionPrototypes)
    {
        Target = target;
        AvailableActionPrototypes = availableActionPrototypes;
    }
}
