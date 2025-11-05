// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Content.Shared.Lua.CLVar;

namespace Content.Client._Lua.ERP;

public sealed class ErpSpawnMenuFilterSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private EntityCategoryPrototype? _erpCategory;

    public override void Initialize()
    {
        base.Initialize();
        _prototypes.TryIndex("Erp", out _erpCategory);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_cfg.GetCVar(CLVars.IsERP)) return;
        if (_erpCategory == null)
        { if (!_prototypes.TryIndex("Erp", out _erpCategory)) return; }
        if (!_ui.TryGetFirstWindow(typeof(EntitySpawnWindow), out var wndBase)) return;
        if (wndBase is not EntitySpawnWindow wnd) return;
        PruneErpButtons(wnd);
    }

    private void PruneErpButtons(Control root)
    {
        for (var i = 0; i < root.ChildCount; i++)
        {
            var child = root.GetChild(i);
            if (child is EntitySpawnButton btn)
            {
                var proto = btn.Prototype;
                if (proto != null && proto.Categories.Contains(_erpCategory!))
                { root.RemoveChild(btn); i -= 1; continue; }
            }
            if (child.ChildCount > 0)
            { PruneErpButtons(child); }
        }
    }
}


