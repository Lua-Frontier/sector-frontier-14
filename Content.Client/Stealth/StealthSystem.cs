using Content.Client.Interactable.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Stealth;

public sealed class StealthSystem : SharedStealthSystem
{
    private static readonly ProtoId<ShaderPrototype> Shader = "Stealth";

    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shader = _protoMan.Index(Shader).InstanceUnique();

        SubscribeLocalEvent<StealthComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StealthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StealthComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    public override void SetEnabled(EntityUid uid, bool value, StealthComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.Enabled == value)
            return;

        base.SetEnabled(uid, value, component);
        UpdateStealthVisuals(uid, component);
    }

    private void OnStartup(EntityUid uid, StealthComponent component, ComponentStartup args)
    {
        UpdateStealthVisuals(uid, component);
    }

    private void OnShutdown(EntityUid uid, StealthComponent component, ComponentShutdown args)
    {
        if (!Terminating(uid))
            DisableStealthVisuals(uid, component);
    }

    private void OnShaderRender(EntityUid uid, StealthComponent component, BeforePostShaderRenderEvent args)
    {
        if (IsFullyHidden(uid, component))
        {
            ApplyFullHide(uid, component, args.Sprite);
            return;
        }

        RestoreSpriteVisibility(uid, component, args.Sprite);
        ApplyStealthShader(uid, component, args.Sprite);

        // Distortion effect uses screen coordinates. If a player moves, the entities appear to move on screen. this
        // makes the distortion very noticeable.

        // So we need to use relative screen coordinates. The reference frame we use is the parent's position on screen.
        // this ensures that if the Stealth is not moving relative to the parent, its relative screen position remains
        // unchanged.
        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid())
            return; // should never happen, but lets not kill the client.
        var parentXform = Transform(parent);
        var reference = args.Viewport.WorldToLocal(_transformSystem.GetWorldPosition(parentXform));
        reference.X = -reference.X;
        var visibility = GetVisibility(uid, component);

        // actual visual visibility effect is limited to +/- 1.
        visibility = Math.Clamp(visibility, -1f, 1f);

        _shader.SetParameter("reference", reference);
        _shader.SetParameter("visibility", visibility);

        visibility = MathF.Max(0, visibility);
        _sprite.SetColor((uid, args.Sprite), new Color(visibility, visibility, 1, 1));
    }

    private bool IsFullyHidden(EntityUid uid, StealthComponent component)
    {
        return component.Enabled && GetVisibility(uid, component) <= -1f;
    }

    private void UpdateStealthVisuals(EntityUid uid, StealthComponent? component = null, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref component, ref sprite, false) || sprite == null)
            return;

        if (IsFullyHidden(uid, component))
        {
            ApplyFullHide(uid, component, sprite);
            return;
        }

        if (!component.Enabled)
        {
            DisableStealthVisuals(uid, component, sprite);
            return;
        }

        RestoreSpriteVisibility(uid, component, sprite);
        ApplyStealthShader(uid, component, sprite);
    }

    private void ApplyFullHide(EntityUid uid, StealthComponent component, SpriteComponent sprite)
    {
        if (sprite.Visible)
        {
            component.HadSpriteVisible = true;
            _sprite.SetVisible((uid, sprite), false);
        }

        sprite.PostShader = null;
        sprite.GetScreenTexture = false;
        sprite.RaiseShaderEvent = false;

        if (TryComp(uid, out InteractionOutlineComponent? outline))
        {
            RemCompDeferred(uid, outline);
            component.HadOutline = true;
        }
    }

    private void ApplyStealthShader(EntityUid uid, StealthComponent component, SpriteComponent sprite)
    {
        _sprite.SetColor((uid, sprite), Color.White);
        sprite.PostShader = _shader;
        sprite.GetScreenTexture = true;
        sprite.RaiseShaderEvent = true;

        if (TryComp(uid, out InteractionOutlineComponent? outline))
        {
            RemCompDeferred(uid, outline);
            component.HadOutline = true;
        }
    }

    private void RestoreSpriteVisibility(EntityUid uid, StealthComponent component, SpriteComponent sprite)
    {
        if (!sprite.Visible && component.HadSpriteVisible)
        {
            _sprite.SetVisible((uid, sprite), true);
            component.HadSpriteVisible = false;
        }
    }

    private void DisableStealthVisuals(EntityUid uid, StealthComponent? component = null, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref component, ref sprite, false) || sprite == null)
            return;

        RestoreSpriteVisibility(uid, component, sprite);

        _sprite.SetColor((uid, sprite), Color.White);
        sprite.PostShader = null;
        sprite.GetScreenTexture = false;
        sprite.RaiseShaderEvent = false;

        if (component.HadOutline && !TerminatingOrDeleted(uid))
            EnsureComp<InteractionOutlineComponent>(uid);
    }
}
