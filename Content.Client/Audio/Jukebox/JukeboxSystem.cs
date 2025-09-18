using Content.Shared.Audio.Jukebox;
using Content.Shared.CCVar; // Lua
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Configuration; // Lua
using Robust.Shared.Audio.Components;

namespace Content.Client.Audio.Jukebox;


public sealed class JukeboxSystem : SharedJukeboxSystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!; // Lua

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JukeboxComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<JukeboxComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        SubscribeLocalEvent<JukeboxComponent, AfterAutoHandleStateEvent>(OnJukeboxAfterState);

        _protoManager.PrototypesReloaded += OnProtoReload;

        // Apply current volume on init and react to changes in real-time
        _cfg.OnValueChanged(CCVars.JukeboxVolume, OnJukeboxVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _protoManager.PrototypesReloaded -= OnProtoReload;
    }

    private void OnProtoReload(PrototypesReloadedEventArgs obj)
    {
        if (!obj.WasModified<JukeboxPrototype>())
            return;

        var query = AllEntityQuery<JukeboxComponent, UserInterfaceComponent>();

        while (query.MoveNext(out var uid, out _, out var ui))
        {
            if (!_uiSystem.TryGetOpenUi<JukeboxBoundUserInterface>((uid, ui), JukeboxUiKey.Key, out var bui))
                continue;

            bui.PopulateMusic();
        }
    }

    private void OnJukeboxAfterState(Entity<JukeboxComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_uiSystem.TryGetOpenUi<JukeboxBoundUserInterface>(ent.Owner, JukeboxUiKey.Key, out var bui))
            return;

        bui.Reload();
    }

    private void OnAnimationCompleted(EntityUid uid, JukeboxComponent component, AnimationCompletedEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
            !_appearanceSystem.TryGetData<JukeboxVisualState>(uid, JukeboxVisuals.VisualState, out var visualState, appearance))
        {
            visualState = JukeboxVisualState.On;
        }

        UpdateAppearance((uid, sprite), visualState, component);
    }

    private void OnAppearanceChange(EntityUid uid, JukeboxComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.AppearanceData.TryGetValue(JukeboxVisuals.VisualState, out var visualStateObject) ||
            visualStateObject is not JukeboxVisualState visualState)
        {
            visualState = JukeboxVisualState.On;
        }

        UpdateAppearance((uid, args.Sprite), visualState, component);
    }

    private void UpdateAppearance(Entity<SpriteComponent> entity, JukeboxVisualState visualState, JukeboxComponent component)
    {
        //SetLayerState(JukeboxVisualLayers.Base, component.OffState, entity); // Lua
        // Lu start
        var vol = _cfg.GetCVar(CCVars.JukeboxVolume);
        if (component.AudioStream != null && TryComp(component.AudioStream, out AudioComponent? audioComp)) Audio.SetGain(component.AudioStream, vol, audioComp);
        _sprite.LayerSetVisible(entity.AsNullable(), JukeboxVisualLayers.Base, true);

        var hasStatic = _sprite.LayerMapTryGet(entity.AsNullable(), JukeboxVisualLayers.OverlayStatic, out _, false);
        var hasDynamic = _sprite.LayerMapTryGet(entity.AsNullable(), JukeboxVisualLayers.OverlayDynamic, out _, false);
        if (!hasStatic && _sprite.LayerMapTryGet(entity.AsNullable(), JukeboxVisualLayers.Overlay, out var _, false))
        { hasStatic = true; }
        var targetLayer = hasDynamic ? JukeboxVisualLayers.OverlayDynamic : (hasStatic ? JukeboxVisualLayers.OverlayStatic : JukeboxVisualLayers.Base);
        // Lua end
        switch (visualState)
        {
            case JukeboxVisualState.On:
                //SetLayerState(JukeboxVisualLayers.Base, component.OnState, entity); // Lua
                // Lua start
                if (hasStatic && !string.IsNullOrEmpty(component.OnOverlayState))
                {
                    _sprite.LayerSetVisible(entity.AsNullable(), JukeboxVisualLayers.OverlayStatic, true);
                    SetLayerState(JukeboxVisualLayers.OverlayStatic, component.OnOverlayState, entity);
                }
                if (hasDynamic)
                {
                    _sprite.LayerSetVisible(entity.AsNullable(), JukeboxVisualLayers.OverlayDynamic, true);
                    SetLayerState(JukeboxVisualLayers.OverlayDynamic, component.OnState, entity);
                }
                else
                { SetLayerState(targetLayer, component.OnState, entity); }
                // Lua end
                break;

            case JukeboxVisualState.Off:
                //SetLayerState(JukeboxVisualLayers.Base, component.OffState, entity); // Lua
                // Lua start
                if (hasStatic) _sprite.LayerSetVisible(entity.AsNullable(), JukeboxVisualLayers.OverlayStatic, false);
                if (hasDynamic)
                {
                    _sprite.LayerSetVisible(entity.AsNullable(), JukeboxVisualLayers.OverlayDynamic, true);
                    SetLayerState(JukeboxVisualLayers.OverlayDynamic, component.OffState, entity);
                }
                else
                { SetLayerState(targetLayer, component.OffState, entity); }
                // Lua end
                break;

            case JukeboxVisualState.Select:
                //PlayAnimation(entity.Owner, JukeboxVisualLayers.Base, component.SelectState, 1.0f, entity); // Lua
                // Lua start
                if (component.SelectIsLoop)
                {
                    if (hasStatic && !string.IsNullOrEmpty(component.OnOverlayState))
                    {
                        _sprite.LayerSetVisible(entity.AsNullable(), JukeboxVisualLayers.OverlayStatic, true);
                        SetLayerState(JukeboxVisualLayers.OverlayStatic, component.OnOverlayState, entity);
                    }
                    if (hasDynamic)
                    {
                        _sprite.LayerSetVisible(entity.AsNullable(), JukeboxVisualLayers.OverlayDynamic, true);
                        SetLayerState(JukeboxVisualLayers.OverlayDynamic, component.SelectState, entity);
                    }
                    else
                    { SetLayerState(targetLayer, component.SelectState, entity); }
                }
                else { PlayAnimation(entity.Owner, targetLayer, component.SelectState, 1.0f, entity); }
                // Lua end
                break;
        }
    }

    // Lua start
    private void OnJukeboxVolumeChanged(float value)
    {
        var query = EntityQueryEnumerator<JukeboxComponent>();
        while (query.MoveNext(out var _, out var comp))
        { if (comp.AudioStream != null && TryComp(comp.AudioStream, out AudioComponent? audio)) Audio.SetGain(comp.AudioStream, value, audio); }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var vol = _cfg.GetCVar(CCVars.JukeboxVolume);
        var query = EntityQueryEnumerator<JukeboxComponent>();
        while (query.MoveNext(out var _, out var comp))
        { if (comp.AudioStream != null && TryComp(comp.AudioStream, out AudioComponent? audio)) Audio.SetGain(comp.AudioStream, vol, audio); }
    }
    // Lua end

    private void PlayAnimation(EntityUid uid, JukeboxVisualLayers layer, string? state, float animationTime, SpriteComponent sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        if (!_animationPlayer.HasRunningAnimation(uid, state))
        {
            var animation = GetAnimation(layer, state, animationTime);
            _sprite.LayerSetVisible((uid, sprite), layer, true);
            _animationPlayer.Play(uid, animation, state);
        }
    }

    private static Animation GetAnimation(JukeboxVisualLayers layer, string state, float animationTime)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(animationTime),
            AnimationTracks =
                {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = layer,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state, 0f)
                        }
                    }
                }
        };
    }

    private void SetLayerState(JukeboxVisualLayers layer, string? state, Entity<SpriteComponent> sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
        _sprite.LayerSetAutoAnimated(sprite.AsNullable(), layer, true);
        _sprite.LayerSetRsiState(sprite.AsNullable(), layer, state);
    }
}
