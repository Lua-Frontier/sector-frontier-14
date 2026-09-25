// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.
// In addition to AGPLv3, the author grants the "Мёртвый Космос" project

using Robust.Shared.Serialization;

namespace Content.Lua.Shared.HardsuitIdentification;

[Serializable, NetSerializable]
public sealed partial class SelectDNAActionEvent : EntityEventArgs
{
    public NetEntity Target;
    public string ActionType;

    public SelectDNAActionEvent(NetEntity target, string actionType)
    {
        Target = target;
        ActionType = actionType;
    }
}
