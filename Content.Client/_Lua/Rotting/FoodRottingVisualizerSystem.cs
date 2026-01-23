using Content.Shared._Lua.Rotting;
using Robust.Client.GameObjects;

namespace Content.Client._Lua.Rotting;

public sealed class FoodRottingVisualizerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoodRottingComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, FoodRottingComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null) return;
        if (!_appearance.TryGetData<Color>(uid, FoodRottingVisuals.Color, out var color, args.Component)) return;
        args.Sprite.Color = color;
    }
}

