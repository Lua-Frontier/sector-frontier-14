using System.Numerics;
using Content.Lua.UIKit.Styles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.Lua.Research.Discovery;
public sealed class DiscoverySignalHuntControl : Control
{
    public Vector2 AimNormalized { get; set; } = new(0.5f, 0.5f);
    public Vector2 SignalNormalized { get; set; } = new(0.5f, 0.5f);
    public bool Locked { get; set; }
    public bool HasSample { get; set; }

    private float _time;

    public DiscoverySignalHuntControl()
    {
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (!HasSample)
            return;

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        var accent = LunaWindowStyle.Accent;
        var good = LunaWindowStyle.AccentGood;
        var pulse = Locked ? good : accent;

        var grid = LunaWindowStyle.Divider.WithAlpha(0.45f);
        for (var g = 1; g < 4; g++)
        {
            var x = size.X * g / 4f;
            var y = size.Y * g / 4f;
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, size.Y), grid);
            handle.DrawLine(new Vector2(0, y), new Vector2(size.X, y), grid);
        }

        var signal = new Vector2(
            MathHelper.Clamp(SignalNormalized.X, 0.08f, 0.92f) * size.X,
            MathHelper.Clamp(SignalNormalized.Y, 0.08f, 0.92f) * size.Y);

        var aim = new Vector2(
            MathHelper.Clamp(AimNormalized.X, 0.08f, 0.92f) * size.X,
            MathHelper.Clamp(AimNormalized.Y, 0.08f, 0.92f) * size.Y);

        var waveColor = Locked ? good : accent;
        var maxR = MathF.Min(size.X, size.Y) * 0.28f;
        for (var i = 0; i < 3; i++)
        {
            var phase = (_time * 0.22f + i * 0.33f) % 1f;
            var radius = 6f + phase * maxR;
            var alpha = Locked ? 0.35f * (1f - phase) : 0.55f * (1f - phase);
            handle.DrawCircle(signal, radius, waveColor.WithAlpha(alpha), false);
        }

        handle.DrawCircle(signal, 5f, waveColor.WithAlpha(Locked ? 0.35f : 0.55f), true);
        handle.DrawCircle(signal, 8f, waveColor.WithAlpha(0.25f), false);
        var a = pulse.WithAlpha(Locked ? 0.95f : 0.65f + 0.15f * MathF.Sin(_time * 2f));
        handle.DrawCircle(aim, 10f, a, false);
        handle.DrawCircle(aim, 12f, a.WithAlpha(a.A * 0.35f), false);
        handle.DrawLine(aim + new Vector2(-14f, 0), aim + new Vector2(-5f, 0), a);
        handle.DrawLine(aim + new Vector2(5f, 0), aim + new Vector2(14f, 0), a);
        handle.DrawLine(aim + new Vector2(0, -14f), aim + new Vector2(0, -5f), a);
        handle.DrawLine(aim + new Vector2(0, 5f), aim + new Vector2(0, 14f), a);

        if (Locked)
        {
            var b = 20f;
            var c = good.WithAlpha(0.8f);
            handle.DrawLine(aim + new Vector2(-b, -b), aim + new Vector2(-b + 7, -b), c);
            handle.DrawLine(aim + new Vector2(-b, -b), aim + new Vector2(-b, -b + 7), c);
            handle.DrawLine(aim + new Vector2(b, -b), aim + new Vector2(b - 7, -b), c);
            handle.DrawLine(aim + new Vector2(b, -b), aim + new Vector2(b, -b + 7), c);
            handle.DrawLine(aim + new Vector2(-b, b), aim + new Vector2(-b + 7, b), c);
            handle.DrawLine(aim + new Vector2(-b, b), aim + new Vector2(-b, b - 7), c);
            handle.DrawLine(aim + new Vector2(b, b), aim + new Vector2(b - 7, b), c);
            handle.DrawLine(aim + new Vector2(b, b), aim + new Vector2(b, b - 7), c);
        }
    }
}
