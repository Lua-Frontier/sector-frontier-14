using System.Numerics;
using Content.Shared._Lua.FtlPoints;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._Lua.FtlPoints;

public sealed class StarmapControl : Control
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    public float Range = 1f;
    private List<Star> _stars = new List<Star>();
    private const float Ppd = 15f;
    private readonly int _hyperlaneNeighbors = 3;
    private const float MaxHyperlaneDistance = 1200f;
    private readonly Font _font;
    public event Action<Star>? OnStarSelect;

    public StarmapControl()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 8);
    }

    public void SetStars(List<Star> stars)
    { _stars = stars; }

    private Vector2 CalculateOffset()
    { return Size / 2; }

    private Vector2 GetMouseLocalUnscaled()
    {
        var scaledMouse = _uiManager.MousePositionScaled.Position;
        var scaledGlobal = GlobalPosition * UIScale;
        return (scaledMouse - scaledGlobal) / UIScale;
    }

    private Vector2 GetPositionOfStar(Vector2 position)
    { return CalculateOffset() + (position * Ppd); }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(new UIBox2(Vector2.Zero, Size), Color.Black);
        var lines = 10;
        for (var i = 0; i < lines; i++)
        {
            var xStep = Size.X / lines;
            var yStep = Size.Y / lines;
            handle.DrawLine(new Vector2(i * xStep, 0), new Vector2(i * xStep, Size.Y), Color.DarkSlateGray);
            handle.DrawLine(new Vector2(0, i * yStep), new Vector2(Size.X, i * yStep), Color.DarkSlateGray);
        }
        handle.DrawCircle(GetPositionOfStar(Vector2.Zero), Range * Ppd, Color.White, false);
        handle.DrawCircle(GetPositionOfStar(Vector2.Zero), Range * Ppd, new Color(47, 79, 79, 127));
        handle.DrawCircle(GetPositionOfStar(Vector2.Zero), (int) (Range * 1.5) * Ppd, Color.Blue, false);
        if (_stars.Count > 1)
        {
            var uiPositions = new Vector2[_stars.Count];
            for (var i = 0; i < _stars.Count; i++) uiPositions[i] = GetPositionOfStar(_stars[i].Position);
            var centerStarIndex = 0;
            var minDistance = float.MaxValue;
            for (var i = 0; i < _stars.Count; i++)
            {
                var distance = Vector2.Distance(_stars[i].Position, Vector2.Zero);
                if (distance < minDistance)
                { minDistance = distance; centerStarIndex = i; }
            }
            var centerPos = uiPositions[centerStarIndex];
            for (var i = 0; i < _stars.Count; i++)
            {
                if (i == centerStarIndex) continue;
                var star = _stars[i];
                if (IsSectorStar(star.Name))
                {
                    var posA = uiPositions[i];
                    var lineColor = new Color(255, 215, 0, 200);
                    handle.DrawLine(centerPos, posA, lineColor);
                }
            }
            var connectedStars = new HashSet<int> { centerStarIndex };
            var remainingStars = new List<int>();
            for (var i = 0; i < _stars.Count; i++)
            { if (i != centerStarIndex) remainingStars.Add(i); }
            while (remainingStars.Count > 0)
            {
                var bestConnection = (fromStar: -1, toStar: -1, distance: float.MaxValue);
                foreach (var remainingStar in remainingStars)
                {
                    var remainingPos = _stars[remainingStar].Position;
                    foreach (var connectedStar in connectedStars)
                    {
                        var connectedPos = _stars[connectedStar].Position;
                        var distance = Vector2.Distance(remainingPos, connectedPos);
                        if (distance <= MaxHyperlaneDistance && distance < bestConnection.distance)
                        { bestConnection = (remainingStar, connectedStar, distance); }
                    }
                }
                if (bestConnection.fromStar != -1)
                {
                    var fromPos = uiPositions[bestConnection.fromStar];
                    var toPos = uiPositions[bestConnection.toStar];
                    var lineColor = IsSectorStar(_stars[bestConnection.fromStar].Name) || IsSectorStar(_stars[bestConnection.toStar].Name) ? new Color(255, 215, 0, 150) : new Color(112, 128, 144, 120);
                    handle.DrawLine(fromPos, toPos, lineColor);
                    connectedStars.Add(bestConnection.fromStar);
                    remainingStars.Remove(bestConnection.fromStar);
                }
                else
                {
                    foreach (var remainingStar in remainingStars)
                    {
                        var remainingPos = uiPositions[remainingStar];
                        var fallbackColor = new Color(112, 128, 144, 80);
                        handle.DrawLine(centerPos, remainingPos, fallbackColor);
                    }
                    break;
                }
            }
        }

        foreach (var star in _stars)
        {
            var uiPosition = GetPositionOfStar(star.Position);
            var localMouse = GetMouseLocalUnscaled();
            var radius = 5f;
            var hovered = Vector2.Distance(localMouse, uiPosition) <= radius * 1.5f;
            var color = Color.White;
            var name = star.Name;
            var isSector = IsSectorStar(star.Name);
            if (Vector2.Distance(Vector2.Zero, star.Position) >= Range) color = Color.Red;
            if (Vector2.Distance(Vector2.Zero, star.Position) >= Range * 1.5)
            {
                color = Color.DarkRed;
                name = Loc.GetString("ship-ftl-tag-oor");
            }
            if (star.Position == Vector2.Zero) color = Color.Blue;
            if (isSector)
            {
                color = GetSectorColor(star.Name);
                radius = GetSectorSize(star.Name);
            }
            if (hovered) { radius = isSector ? 12f : 10f; }
            if (isSector)
            {
                handle.DrawCircle(uiPosition, radius + 2, color with { A = 100 }, false);
                handle.DrawCircle(uiPosition, radius, color);
                handle.DrawCircle(uiPosition, radius - 2, Color.White with { A = 150 });
            }
            else
            { handle.DrawCircle(uiPosition, radius, color); }
            if (hovered)
            { handle.DrawString(_font, uiPosition + new Vector2(10, 0), name); }
            if (!hovered || !_inputManager.IsKeyDown(Keyboard.Key.MouseLeft)) continue;
            if (Vector2.Distance(Vector2.Zero, star.Position) >= Range) continue;
            OnStarSelect?.Invoke(star);
        }
    }

    private bool IsSectorStar(string starName)
    {
        return starName == "Сектор Фронтир" ||
               starName == "Поле Астероидов" ||
               starName == "Сектор Наёмников" ||
               starName == "Сектор Пиратов" ||
               starName == "Сектор Нордфолл";
    }

    private Color GetSectorColor(string sectorName)
    {
        return sectorName switch
        {
            "Сектор Фронтир" => Color.FromHex("#d3ffa0"),
            "Поле Астероидов" => Color.FromHex("#c3cf18"),
            "Сектор Наёмников" => Color.FromHex("#182faf"),
            "Сектор Пиратов" => Color.FromHex("#8A6642"),
            "Сектор Нордфолл" => Color.FromHex("#990816"),
            _ => Color.White
        };
    }

    private float GetSectorSize(string sectorName)
    {
        return sectorName switch
        {
            "Сектор Фронтир" => 9f,
            _ => 7f
        };
    }
}
