// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Shared.Configuration;
using Robust.Shared.Map.Events;
using Robust.Shared.Prototypes;
using Content.Shared.Lua.CLVar;

namespace Content.Server._Lua.ERP;

public sealed class ERPMapFilterSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeforeEntityReadEvent>(OnBeforeEntityRead);
    }

    private void OnBeforeEntityRead(BeforeEntityReadEvent ev)
    {
        if (_cfg.GetCVar(CLVars.IsERP)) return;
        if (!_prototypes.TryIndex<EntityCategoryPrototype>("Erp", out var erpCategory)) return;
        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.Categories.Contains(erpCategory)) continue;
            ev.DeletedPrototypes.Add(proto.ID);
        }
    }
}


