// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.StationTeleporter;
using Content.Shared._Lua.StationTeleporter.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Lua.StationTeleporter;

public sealed class StationTeleporterSystem : SharedStationTeleporterSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationTeleporterComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<StationTeleporterComponent> ent, ref AppearanceChangeEvent args)
    {
        if (ent.Comp.PortalLayerMap is null) return;
        if (!_appearance.TryGetData<Color>(ent, TeleporterPortalVisuals.Color, out var newColor, args.Component)) return;
        if (!TryComp<SpriteComponent>(ent, out var sprite)) return;
        if (!sprite.LayerMapTryGet(ent.Comp.PortalLayerMap, out var index)) return;
        sprite.LayerSetColor(index, newColor);
    }
}
