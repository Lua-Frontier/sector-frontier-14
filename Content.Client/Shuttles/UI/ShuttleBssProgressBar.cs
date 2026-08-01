// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client.Shuttles.UI;

public sealed class ShuttleBssProgressBar : Control
{
    private readonly Font _font;
    private float _progress = 1f;
    private Color _barColor = Color.FromHex("#80C71F");

    public ShuttleBssProgressBar()
    {
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 9);
        MinSize = new Vector2(100, 14);
    }

    public void SetProgress(float progress)
    {
        _progress = MathHelper.Clamp(progress, 0f, 1f);
    }

    public void SetColor(Color color)
    {
        _barColor = color;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var bounds = new UIBox2(0, 0, PixelWidth, PixelHeight);
        handle.DrawRect(bounds, Color.FromHex("#081015"));

        var inner = new UIBox2(2, 2, MathF.Max(2, PixelWidth - 2), MathF.Max(2, PixelHeight - 2));
        var fillWidth = MathF.Max(0, (PixelWidth - 4) * _progress);
        if (fillWidth > 0)
            handle.DrawRect(new UIBox2(2, 2, 2 + fillWidth, MathF.Max(2, PixelHeight - 2)), _barColor);

        var tickColor = Color.FromHex("#A9C8CC55");
        for (var i = 1; i < 4; i++)
        {
            var x = PixelWidth * i / 4f;
            handle.DrawLine(new Vector2(x, 3), new Vector2(x, PixelHeight - 3), tickColor);
        }

        handle.DrawRect(inner, Color.FromHex("#D8F6F455"), filled: false);
        handle.DrawRect(bounds, Color.FromHex("#25454E"), filled: false);

        var label = $"{(int) MathF.Round(_progress * 100f)}%";
        var dimensions = handle.GetDimensions(_font, label, UIScale);
        var textPos = new Vector2((PixelWidth - dimensions.X) * 0.5f, (PixelHeight - dimensions.Y) * 0.5f);
        handle.DrawString(_font, textPos, label, UIScale, Color.FromHex("#EDF7F6"));
    }
}
