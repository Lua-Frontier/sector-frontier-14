// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Content.Shared._Lua.AmbientSpaceEffects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.AmbientSpaceEffects;

public sealed class AmbientSpaceEffectSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private AmbientSpaceEffectOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new AmbientSpaceEffectOverlay(EntityManager, _prototypes, _cfg);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }
}
