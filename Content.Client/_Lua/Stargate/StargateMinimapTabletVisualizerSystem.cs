// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Stargate;
using Content.Shared._Lua.Stargate.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Lua.Stargate;

public sealed class StargateMinimapTabletVisualizerSystem : VisualizerSystem<StargateMinimapTabletComponent>
{
    private const string StateWithDisk = "pda-map-on";
    private const string StateNoDisk = "pda-map-off";

    protected override void OnAppearanceChange(EntityUid uid, StargateMinimapTabletComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<bool>(uid, StargateMinimapTabletVisuals.HasDisk, out var hasDisk, args.Component))
            return;

        var state = hasDisk ? StateWithDisk : StateNoDisk;
        args.Sprite.LayerSetState(0, state);
    }
}
