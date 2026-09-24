// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
//This content is sourced from Мёртвый Космос and is used with explicit permission for use in Sector Frontier(LuaWorld) https://github.com/HacksLua/sector-frontier-14.
// Мёртвый Космос - This file is licensed under AGPLv3
// Copyright (c) 2025 Мёртвый Космос Contributors
// See AGPLv3.txt for details.

using Content.Shared.Examine;

namespace Content.Lua.Server._DeadSpace.Photocopier;

public sealed class TonerCartridgeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TonerCartridgeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, TonerCartridgeComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushText(component.CurrentAmount == 0 ? Loc.GetString("toner-component-examine-empty")
            : Loc.GetString("toner-component-examine", ("left", component.CurrentAmount), ("max", component.MaxAmount)));
    }
}
