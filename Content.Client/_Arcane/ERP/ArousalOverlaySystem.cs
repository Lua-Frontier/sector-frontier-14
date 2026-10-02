using Content.Shared._Arcane.ERP;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Arcane.ERP;

public sealed class ArousalOverlaySystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private ArousalOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ArousalOverlay();

        SubscribeLocalEvent<ArousalComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<ArousalComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<ArousalComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ArousalComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _configuration.OnValueChanged(
            CCVars.ReducedMotion,
            OnReducedMotionChanged,
            invokeImmediately: true);
    }

    public override void Shutdown()
    {
        _configuration.UnsubValueChanged(CCVars.ReducedMotion, OnReducedMotionChanged);
        _overlayManager.RemoveOverlay(_overlay);
        _overlay.Dispose();

        base.Shutdown();
    }

    private void OnComponentStartup(Entity<ArousalComponent> ent, ref ComponentStartup args)
    {
        TryAddOverlay(ent.Owner);
    }

    private void OnComponentShutdown(Entity<ArousalComponent> ent, ref ComponentShutdown args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
            _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<ArousalComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        TryAddOverlay(ent.Owner);
    }

    private void OnPlayerDetached(Entity<ArousalComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
    }

    private void OnReducedMotionChanged(bool reducedMotion)
    {
        _overlay.MotionScale = reducedMotion ? 0f : 1f;
    }

    private void TryAddOverlay(EntityUid player)
    {
        if (_playerManager.LocalEntity != player || _overlayManager.HasOverlay<ArousalOverlay>())
            return;

        _overlayManager.AddOverlay(_overlay);
    }
}
