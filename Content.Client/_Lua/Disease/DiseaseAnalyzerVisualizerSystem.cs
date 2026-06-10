// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using Robust.Client.GameObjects;
using Content.Shared._Lua.Disease.Components;

namespace Content.Client._Lua.Disease;

public sealed class DiseaseAnalyzerVisualizerSystem : VisualizerSystem<DiseaseAnalyzerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DiseaseAnalyzerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<bool>(uid, DiseaseAnalyzerVisuals.IsOn, out var isOn, args.Component))
            SpriteSystem.LayerSetVisible((uid, args.Sprite), DiseaseAnalyzerVisualLayers.IsOn, isOn);
    }
}

public enum DiseaseAnalyzerVisualLayers : byte
{
    IsOn,
    IsPrinting
}
