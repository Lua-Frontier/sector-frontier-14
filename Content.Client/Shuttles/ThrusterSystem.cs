using Content.Shared.Shuttles.Components;
using Content.Shared.Tag;
using Robust.Client.GameObjects;

namespace Content.Client.Shuttles;

/// <summary>
/// Handles making a thruster visibly turn on/emit an exhaust plume according to its state.
/// </summary>
public sealed class ThrusterSystem : VisualizerSystem<ThrusterComponent>
{
    [Dependency] private readonly TagSystem _tag = default!;
    /// <summary>
    /// Updates whether or not the thruster is visibly active/thrusting.
    /// </summary>
    protected override void OnAppearanceChange(EntityUid uid, ThrusterComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
        || !AppearanceSystem.TryGetData<bool>(uid, ThrusterVisualState.State, out var state, args.Component))
            return;

        SpriteSystem.LayerSetVisible((uid, args.Sprite), ThrusterVisualLayers.ThrustOn, state);
        var thrusting = state && AppearanceSystem.TryGetData<bool>(uid, ThrusterVisualState.Thrusting, out var t, args.Component) && t;
        SetThrusting(uid, thrusting, args.Sprite);
        var alwaysBurn = _tag.HasTag(uid, "OmniBurnAlwaysOn");
        if (alwaysBurn && SpriteSystem.LayerMapTryGet((uid, args.Sprite), ThrusterVisualLayers.ThrustingUnshaded, out var unshadedLayer, false))
        { SpriteSystem.LayerSetVisible((uid, args.Sprite), unshadedLayer, state || thrusting); }
    }

    /// <summary>
    /// Sets whether or not the exhaust plume of the thruster is visible or not.
    /// </summary>
    private void SetThrusting(EntityUid uid, bool value, SpriteComponent sprite)
    {
        if (SpriteSystem.LayerMapTryGet((uid, sprite), ThrusterVisualLayers.Thrusting, out var thrustingLayer, false))
        {
            SpriteSystem.LayerSetVisible((uid, sprite), thrustingLayer, value);
        }

        if (SpriteSystem.LayerMapTryGet((uid, sprite), ThrusterVisualLayers.ThrustingUnshaded, out var unshadedLayer, false))
        {
            SpriteSystem.LayerSetVisible((uid, sprite), unshadedLayer, value);
        }
    }
}

public enum ThrusterVisualLayers : byte
{
    Base,
    ThrustOn,
    Thrusting,
    ThrustingUnshaded,
}
