using System.Numerics;
using System.Text;
using Content.Client.Resources;
using Content.Client._Lua.Styles;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Crescent.SpaceBiomes;

/// <summary>
/// this system handles the actual drawing of grid names, descriptions, and biome overlays & descriptions
/// </summary>
public sealed class SpaceBiomeTextOverlay : Overlay
{
    private const float MotdCharsPerSecond = 45f;
    private const float MotdMinHoldSeconds = 15f;
    private const float MotdMaxHoldSeconds = 60f;
    private const float MotdSecondsPerChar = 1f / 8f;
    private const float MotdWindowWidth = 900f;
    private const float MotdWindowHeight = 520f;
    private const float MotdContentPadding = 20f;
    private const float MotdHotbarClearance = 100f;
    private const float MotdTitleClearance = 110f + 140f + 40f;

    private enum MotdPhase : byte
    {
        None,
        Typing,
        Holding,
    }

    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;
    private readonly Font _font;
    private readonly Font _descriptionfont;
    private readonly Font _motdFont;
    private readonly Font _motdHintFont;

    public string? Text;
    public int Index;
    public bool Reverse;
    public Vector2 Position;
    public TimeSpan CharInterval;
    private TimeSpan _nextUpd = TimeSpan.Zero;

    public string? TextDescription;
    public TimeSpan CharIntervalDescription;
    public int IndexDescription;
    public bool ReverseDescription;
    public Vector2 PositionDescription;
    private TimeSpan _nextUpdDescription = TimeSpan.Zero;

    public string? MotdText;
    public int MotdIndex;
    private MotdPhase _motdPhase = MotdPhase.None;
    private float _motdContentWidth;
    private Vector2 _motdOrigin;
    private float _motdContentBottom;
    private TimeSpan _motdNextUpd = TimeSpan.Zero;
    private TimeSpan _motdCharInterval = TimeSpan.Zero;
    private TimeSpan _motdHoldUntil = TimeSpan.Zero;
    private string _motdDismissHint = string.Empty;

    public bool IsMotdActive => _motdPhase != MotdPhase.None && !string.IsNullOrEmpty(MotdText);

    public SpaceBiomeTextOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = _cache.GetFont("/Fonts/Doloto/Doloto-Regular.ttf", 75); //Lua Iceberg -> Doloto
        _descriptionfont = _cache.GetFont("/Fonts/Doloto/Doloto-Regular.ttf", 30); //Lua Iceberg -> Doloto
        _motdFont = LunaWindowStyle.FontBody;
        _motdHintFont = LunaWindowStyle.FontSmall;
    }

    public void Reset()
    {
        Text = null;
        Index = 0;
        Reverse = false;
        Position = Vector2.Zero;
        _nextUpd = TimeSpan.Zero;
    }

    public void ResetDescription()
    {
        TextDescription = null;
        IndexDescription = 0;
        ReverseDescription = false;
        PositionDescription = Vector2.Zero;
        _nextUpdDescription = TimeSpan.Zero;
    }

    public void ResetMotd()
    {
        MotdText = null;
        MotdIndex = 0;
        _motdPhase = MotdPhase.None;
        _motdContentWidth = 0f;
        _motdOrigin = Vector2.Zero;
        _motdContentBottom = 0f;
        _motdNextUpd = TimeSpan.Zero;
        _motdCharInterval = TimeSpan.Zero;
        _motdHoldUntil = TimeSpan.Zero;
        _motdDismissHint = string.Empty;
    }

    public void ShowMotd(string text, Vector2 viewportSize)
    {
        ResetMotd();

        if (string.IsNullOrWhiteSpace(text))
            return;

        LayoutMotdArea(viewportSize, out var contentWidth, out var origin, out var contentBottom);
        _motdContentWidth = contentWidth;
        _motdOrigin = origin;
        _motdContentBottom = contentBottom;

        MotdText = LunaWindowStyle.WrapText(text.Trim(), _motdFont, contentWidth);
        _motdCharInterval = TimeSpan.FromSeconds(1f / MotdCharsPerSecond);
        _motdPhase = MotdPhase.Typing;
        _motdDismissHint = Loc.GetString("company-briefing-overlay-dismiss");
    }

    private static void LayoutMotdArea(Vector2 viewport, out float contentWidth, out Vector2 origin, out float contentBottom)
    {
        var windowW = MathF.Min(MotdWindowWidth, MathF.Max(280f, viewport.X - MotdContentPadding * 2f));
        var topMin = MotdTitleClearance;
        var bottomMax = MathF.Max(topMin + 180f, viewport.Y - MotdHotbarClearance);
        var availableH = bottomMax - topMin;
        var windowH = MathF.Min(MotdWindowHeight, availableH);
        var left = (viewport.X - windowW) * 0.5f;
        var top = topMin;

        contentWidth = MathF.Max(160f, windowW - MotdContentPadding * 2f);
        origin = new Vector2(left + MotdContentPadding, top + MotdContentPadding);
        contentBottom = top + windowH - MotdContentPadding;
    }

    public void HandleMotdDismissInput()
    {
        if (!IsMotdActive)
            return;

        if (_motdPhase == MotdPhase.Typing)
            BeginMotdHold();
        else
            ResetMotd();
    }

    public void DismissMotd() => ResetMotd();

    protected override void Draw(in OverlayDrawArgs args)
    {
        DrawTitle(args);
        DrawDescription(args);
        DrawMotd(args);
    }

    private void DrawTitle(in OverlayDrawArgs args)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        if (Position == Vector2.Zero)
            Position = CalcPosition(_font, Text, new Vector2(args.ViewportBounds.Width, args.ViewportBounds.Height));

        var visible = Text[..Math.Clamp(Index, 0, Text.Length)];
        args.ScreenHandle.DrawString(_font, Position, visible);

        if (_nextUpd > _timing.CurTime)
            return;

        if (!Reverse && Index == Text.Length)
        {
            Reverse = true;
            _nextUpd += TimeSpan.FromSeconds(2);
            Index++;
        }

        if (Reverse && Index == 0)
        {
            Reset();
            return;
        }

        Index = Reverse ? Index - 1 : Index + 1;

        if (_nextUpd == TimeSpan.Zero)
            _nextUpd = _timing.CurTime;
        _nextUpd += CharInterval;
    }

    private void DrawDescription(in OverlayDrawArgs args)
    {
        if (string.IsNullOrEmpty(TextDescription))
            return;

        if (PositionDescription == Vector2.Zero)
            PositionDescription = CalcPositionDescription(_descriptionfont, TextDescription, new Vector2(args.ViewportBounds.Width, args.ViewportBounds.Height));

        var visible = TextDescription[..Math.Clamp(IndexDescription, 0, TextDescription.Length)];
        args.ScreenHandle.DrawString(_descriptionfont, PositionDescription, visible, Color.DarkGray);

        if (_nextUpdDescription > _timing.CurTime)
            return;

        if (!ReverseDescription && IndexDescription == TextDescription.Length)
        {
            ReverseDescription = true;
            _nextUpdDescription += TimeSpan.FromSeconds(2);
            IndexDescription++;
        }

        if (ReverseDescription && IndexDescription == 0)
        {
            ResetDescription();
            return;
        }

        IndexDescription = ReverseDescription ? IndexDescription - 1 : IndexDescription + 1;

        if (_nextUpdDescription == TimeSpan.Zero)
            _nextUpdDescription = _timing.CurTime;
        _nextUpdDescription += CharIntervalDescription;
    }

    private void DrawMotd(in OverlayDrawArgs args)
    {
        if (string.IsNullOrEmpty(MotdText) || _motdPhase == MotdPhase.None)
            return;

        var viewport = new Vector2(args.ViewportBounds.Width, args.ViewportBounds.Height);
        if (_motdOrigin == Vector2.Zero)
            LayoutMotdArea(viewport, out _motdContentWidth, out _motdOrigin, out _motdContentBottom);

        var visible = MotdText[..Math.Clamp(MotdIndex, 0, MotdText.Length)];
        var y = _motdOrigin.Y;
        var lineHeight = _motdFont.GetHeight(1f) + 2f;
        var hintHeight = _motdHintFont.GetHeight(1f) + lineHeight;
        var textBottomLimit = _motdContentBottom - hintHeight;

        foreach (var line in visible.Replace("\r\n", "\n").Split('\n'))
        {
            if (y + lineHeight > textBottomLimit)
                break;

            DrawSoftString(args.ScreenHandle, _motdFont, new Vector2(_motdOrigin.X, y), line, Color.White);
            y += lineHeight;
        }

        if (_motdPhase == MotdPhase.Holding)
        {
            var hintWidth = MeasureWidth(_motdHintFont, _motdDismissHint);
            var hintX = _motdOrigin.X + MathF.Max(0f, (_motdContentWidth - hintWidth) * 0.5f);
            var hintY = _motdContentBottom - _motdHintFont.GetHeight(1f);
            DrawSoftString(args.ScreenHandle, _motdHintFont, new Vector2(hintX, hintY), _motdDismissHint, Color.LightGray);

            if (_timing.CurTime >= _motdHoldUntil)
                ResetMotd();

            return;
        }

        // Typing phase
        if (_motdNextUpd > _timing.CurTime)
            return;

        if (MotdIndex >= MotdText.Length)
        {
            BeginMotdHold();
            return;
        }

        MotdIndex++;

        if (_motdNextUpd == TimeSpan.Zero)
            _motdNextUpd = _timing.CurTime;
        _motdNextUpd += _motdCharInterval;
    }

    private void BeginMotdHold()
    {
        if (string.IsNullOrEmpty(MotdText))
        {
            ResetMotd();
            return;
        }

        MotdIndex = MotdText.Length;
        _motdPhase = MotdPhase.Holding;
        var holdSeconds = Math.Clamp(MotdText.Length * MotdSecondsPerChar, MotdMinHoldSeconds, MotdMaxHoldSeconds);
        _motdHoldUntil = _timing.CurTime + TimeSpan.FromSeconds(holdSeconds);
    }

    private static void DrawSoftString(DrawingHandleScreen handle, Font font, Vector2 pos, string text, Color color)
    {
        handle.DrawString(font, pos + new Vector2(1f, 1f), text, Color.Black.WithAlpha(0.55f * color.A));
        handle.DrawString(font, pos, text, color);
    }

    private Vector2 CalcPosition(Font font, string str, Vector2 viewport)
    {
        Vector2 strSize = new();
        foreach (Rune r in str)
        {
            if (font.TryGetCharMetrics(r, 1, out var metrics))
            {
                strSize.X += metrics.Width;
                strSize.Y = Math.Max(strSize.Y, metrics.Height);
            }
        }

        return new Vector2((viewport.X - strSize.X) / 2, strSize.Y + 110);
    }

    private Vector2 CalcPositionDescription(Font font, string str, Vector2 viewport)
    {
        Vector2 strSize = new();
        foreach (Rune r in str)
        {
            if (font.TryGetCharMetrics(r, 1, out var metrics))
            {
                strSize.X += metrics.Width;
                strSize.Y = Math.Max(strSize.Y, metrics.Height);
            }
        }

        return new Vector2((viewport.X - strSize.X) / 2, strSize.Y + 110 + 140);
    }

    private static float MeasureWidth(Font font, string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!font.TryGetCharMetrics(rune, 1f, out var metrics))
                continue;
            width += metrics.Advance;
        }

        return width;
    }
}
