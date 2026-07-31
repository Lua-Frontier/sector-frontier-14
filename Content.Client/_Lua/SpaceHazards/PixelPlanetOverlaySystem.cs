// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Lua.SpaceHazards;

public sealed class PixelPlanetOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private PixelPlanetOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new PixelPlanetOverlay(EntityManager, _prototypes);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }
}
