
using Content.Shared.Abilities;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.GameObjects;

namespace Content.Client._Lua.Abilities;

/// <summary>
/// Client system that provides a very dim, always-on light around entities with
/// SoftDarkVision to slightly improve visibility in darkness, with a gentle falloff.
/// </summary>
public sealed class SoftDarkVisionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    private EntityUid? _innerLight;
    private EntityUid? _outerLight;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoftDarkVisionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SoftDarkVisionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SoftDarkVisionComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<SoftDarkVisionComponent, LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnInit(EntityUid uid, SoftDarkVisionComponent comp, ComponentInit args)
    {
        if (_player.LocalEntity != uid)
            return;

        EnsureLights(uid, comp);
    }

    private void OnShutdown(EntityUid uid, SoftDarkVisionComponent comp, ComponentShutdown args)
    {
        if (_player.LocalEntity != uid)
            return;

        DeleteLights();
    }

    private void OnAttached(EntityUid uid, SoftDarkVisionComponent comp, LocalPlayerAttachedEvent args)
    {
        EnsureLights(uid, comp);
    }

    private void OnDetached(EntityUid uid, SoftDarkVisionComponent comp, LocalPlayerDetachedEvent args)
    {
        DeleteLights();
    }

    private void EnsureLights(EntityUid owner, SoftDarkVisionComponent comp)
    {
        // Inner bright core
        _innerLight ??= _ent.SpawnAttachedTo(null, Transform(owner).Coordinates);
        _ent.System<TransformSystem>().SetParent(_innerLight.Value, owner);
        var inner = _ent.EnsureComponent<PointLightComponent>(_innerLight.Value);
        _light.SetRadius(_innerLight.Value, comp.PerfectRadius, inner);
        _light.SetEnergy(_innerLight.Value, comp.InnerEnergy, inner);
        _light.SetColor(_innerLight.Value, comp.Color, inner);
        _light.SetSoftness(_innerLight.Value, 1.0f, inner);
        _light.SetCastShadows(_innerLight.Value, false, inner);

        // Outer dim falloff ring
        _outerLight ??= _ent.SpawnAttachedTo(null, Transform(owner).Coordinates);
        _ent.System<TransformSystem>().SetParent(_outerLight.Value, owner);
        var outer = _ent.EnsureComponent<PointLightComponent>(_outerLight.Value);
        _light.SetRadius(_outerLight.Value, comp.FalloffRadius, outer);
        _light.SetEnergy(_outerLight.Value, comp.OuterEnergy, outer);
        _light.SetColor(_outerLight.Value, comp.Color, outer);
        _light.SetSoftness(_outerLight.Value, 2.0f, outer);
        _light.SetCastShadows(_outerLight.Value, false, outer);
    }

    private void DeleteLights()
    {
        if (_innerLight != null)
        {
            _ent.DeleteEntity(_innerLight.Value);
            _innerLight = null;
        }

        if (_outerLight != null)
        {
            _ent.DeleteEntity(_outerLight.Value);
            _outerLight = null;
        }
    }
}
