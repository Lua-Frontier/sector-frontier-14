// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Linq;
using System.Numerics;
using Content.Shared.Holopad;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Lua.Announce;

public sealed class HolopadPortraitSpriteView : EntityPrototypeView
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private IRenderTexture? _renderTarget;
    private ShaderInstance? _shader;
    private EntityUid _shaderEntity = EntityUid.Invalid;
    private readonly bool _forceHolopadEffect;

    public HolopadPortraitSpriteView(IEntityManager entMan, bool forceHolopadEffect = false) : base(null, entMan)
    {
        _forceHolopadEffect = forceHolopadEffect;
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(IRenderHandle renderHandle)
    {
        var entity = Entity;
        if (entity == null)
            return;

        var uid = entity.Value.Owner;
        if (EntMan.Deleted(uid))
            return;

        var hasHologram = EntMan.TryGetComponent<HolopadHologramComponent>(uid, out var holo);
        if (!hasHologram && !_forceHolopadEffect)
        {
            base.Draw(renderHandle);
            return;
        }

        var pixelSize = PixelSize;
        if (pixelSize.X <= 0 || pixelSize.Y <= 0)
            return;

        var targetSize = new Vector2i(Math.Max((int)pixelSize.X, 1), Math.Max((int)pixelSize.Y, 1));
        EnsureRenderTarget(targetSize);

        var screen = renderHandle.DrawingHandleScreen;
        screen.RenderInRenderTarget(_renderTarget!, () => base.Draw(renderHandle), Color.Transparent);

        UpdateShader(uid, entity.Value.Comp1, holo);

        var oldShader = screen.GetShader();
        var modulate = Modulate * ActualModulateSelf;
        screen.UseShader(_shader);
        screen.DrawTextureRect(_renderTarget!.Texture, PixelSizeBox, modulate);
        screen.UseShader(oldShader);
    }

    private void EnsureRenderTarget(Vector2i size)
    {
        if (_renderTarget != null && _renderTarget.Size == size)
            return;

        _renderTarget?.Dispose();
        _renderTarget = _clyde.CreateRenderTarget(
            size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true),
            new TextureSampleParameters { Filter = true },
            nameof(HolopadPortraitSpriteView));
    }

    private void UpdateShader(EntityUid uid, SpriteComponent sprite, HolopadHologramComponent? holo)
    {
        if (_shader == null || _shaderEntity != uid)
        {
            _shader = _prototypes.Index<ShaderPrototype>(holo?.ShaderName ?? "Hologram").InstanceUnique();
            _shaderEntity = uid;
        }

        var texHeight = 1f;
        foreach (var layer in sprite.AllLayers)
            texHeight = MathF.Max(texHeight, layer.PixelSize.Y);

        var color1 = holo?.Color1 ?? Color.FromHex("#65b8e2");
        var color2 = holo?.Color2 ?? Color.FromHex("#3a6981");
        _shader.SetParameter("color1", new Vector3(color1.R, color1.G, color1.B));
        _shader.SetParameter("color2", new Vector3(color2.R, color2.G, color2.B));
        _shader.SetParameter("alpha", holo?.Alpha ?? 0.9f);
        _shader.SetParameter("intensity", holo?.Intensity ?? 2f);
        _shader.SetParameter("texHeight", texHeight);
        _shader.SetParameter("t", (float)_timing.CurTime.TotalSeconds * (holo?.ScrollRate ?? 0.125f));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _renderTarget?.Dispose();
        _renderTarget = null;
    }
}
