using System.Linq;
using System.Numerics;
using Content.Lua.UIKit.Styles;
using Content.Lua.Shared.Research.Discovery;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Content.Shared.Research.Discovery;

namespace Content.Client.Lua.Research.Discovery;

[UsedImplicitly]
public sealed class ServerTechReconfiguratorBoundUserInterface : BoundUserInterface
{
    private ServerTechReconfiguratorWindow? _window;

    public ServerTechReconfiguratorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ServerTechReconfiguratorWindow>();
        _window.SelectTechnology += id => SendMessage(new ServerTechSelectTechnologyMessage(id));
        _window.StartTheft += () => SendMessage(new ServerTechStartTheftMessage());
        _window.SubmitRoute += (pair, cells) => SendMessage(new ServerTechSubmitRouteMessage(pair, cells));
        _window.ResetPuzzle += () => SendMessage(new ServerTechResetPuzzleMessage());
        _window.EjectDisk += () => SendMessage(new ServerTechEjectDiskMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ServerTechReconfiguratorBoundUserInterfaceState theft)
            _window?.UpdateState(theft);
    }
}

public sealed class ServerTechReconfiguratorWindow : LunaWindow
{
    public event Action<string>? SelectTechnology;
    public event Action? StartTheft;
    public event Action<int, List<Vector2i>>? SubmitRoute;
    public event Action? ResetPuzzle;
    public event Action? EjectDisk;

    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private readonly RichTextLabel _status;
    private LineEdit _search = null!;
    private BoxContainer _techList = null!;
    private Label _selected = null!;
    private Button _start = null!;
    private readonly Button _reset;
    private readonly Button _eject;
    private readonly Label _puzzleHelp;
    private readonly ServerTechRoutingControl _routing;
    private readonly BoxContainer _selectionView;
    private readonly BoxContainer _puzzleView;
    private readonly BoxContainer _downloadView;
    private readonly EntityPrototypeView _downloadIcon;
    private readonly Label _downloadName;
    private readonly ProgressBar _downloadProgress;
    private readonly Label _downloadProgressLabel;

    private ServerTechReconfiguratorBoundUserInterfaceState? _state;
    private string _query = string.Empty;

    public ServerTechReconfiguratorWindow()
    {
        Title = Loc.GetString("server-tech-reconfigurator-title");
        MinSize = new Vector2(390, 630);
        SetSize = new Vector2(720, 630);
        ApplyLunaChrome();

        var shell = new PanelContainer
        {
            PanelOverride = LunaWindowStyle.Shell(),
            Margin = new Thickness(4, 2, 4, 4),
        };
        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 7,
            Margin = new Thickness(9, 7, 9, 9),
        };

        var heading = new Label { Text = Loc.GetString("server-tech-reconfigurator-title") };
        LunaWindowStyle.StyleHeading(heading);
        root.AddChild(heading);
        var divider = new PanelContainer { MinHeight = 1, MaxHeight = 1 };
        LunaWindowStyle.StyleDivider(divider);
        root.AddChild(divider);

        _status = new RichTextLabel();
        root.AddChild(_status);

        _selectionView = BuildSelectionView();
        root.AddChild(_selectionView);

        _puzzleView = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            VerticalExpand = true,
            Visible = false,
        };
        _puzzleHelp = new Label
        {
            Text = Loc.GetString("server-tech-reconfigurator-connect-pairs"),
            HorizontalAlignment = HAlignment.Center,
        };
        LunaWindowStyle.StyleSecondary(_puzzleHelp);
        _puzzleView.AddChild(_puzzleHelp);
        _routing = new ServerTechRoutingControl
        {
            SetSize = new Vector2(350, 350),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            VerticalExpand = true,
        };
        _routing.RouteCompleted += (pair, cells) => SubmitRoute?.Invoke(pair, cells);
        _puzzleView.AddChild(_routing);
        _reset = new Button
        {
            Text = Loc.GetString("server-tech-reconfigurator-reset"),
            MinHeight = 32,
        };
        LunaWindowStyle.ApplyCompactStyle(_reset);
        _reset.OnPressed += _ => ResetPuzzle?.Invoke();
        _puzzleView.AddChild(_reset);
        root.AddChild(_puzzleView);

        _downloadView = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 12,
            VerticalExpand = true,
            Visible = false,
        };
        var downloadPanel = new PanelContainer
        {
            PanelOverride = LunaWindowStyle.Panel(),
            HorizontalExpand = true,
            Margin = new Thickness(0, 8),
        };
        var downloadCol = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Margin = new Thickness(12),
        };
        _downloadIcon = new EntityPrototypeView
        {
            Scale = new Vector2(2.2f, 2.2f),
            SetSize = new Vector2(72, 72),
            HorizontalAlignment = HAlignment.Center,
        };
        _downloadName = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleHeading(_downloadName);
        _downloadProgress = new ProgressBar();
        LunaWindowStyle.StyleProgressConfirm(_downloadProgress, 18f);
        _downloadProgressLabel = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleSecondary(_downloadProgressLabel);
        downloadCol.AddChild(_downloadIcon);
        downloadCol.AddChild(_downloadName);
        downloadCol.AddChild(_downloadProgress);
        downloadCol.AddChild(_downloadProgressLabel);
        downloadPanel.AddChild(downloadCol);
        _downloadView.AddChild(downloadPanel);
        var downloadHint = new Label
        {
            Text = Loc.GetString("server-tech-reconfigurator-downloading-hint"),
            HorizontalAlignment = HAlignment.Center,
        };
        LunaWindowStyle.StyleFooter(downloadHint);
        _downloadView.AddChild(downloadHint);
        root.AddChild(_downloadView);

        _eject = new Button
        {
            Text = Loc.GetString("discovery-research-eject-button"),
            HorizontalExpand = true,
            MinHeight = 32,
        };
        LunaWindowStyle.ApplyCompactStyle(_eject);
        _eject.OnPressed += _ => EjectDisk?.Invoke();
        root.AddChild(_eject);

        shell.AddChild(root);
        ContentsContainer.AddChild(shell);
    }

    private BoxContainer BuildSelectionView()
    {
        var view = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 7,
            VerticalExpand = true,
        };
        _search = new LineEdit
        {
            PlaceHolder = Loc.GetString("server-tech-reconfigurator-search"),
            HorizontalExpand = true,
        };
        _search.OnTextChanged += args =>
        {
            _query = args.Text.Trim();
            RebuildTechnologyList();
        };
        view.AddChild(_search);

        var listPanel = new PanelContainer
        {
            PanelOverride = LunaWindowStyle.Panel(),
            VerticalExpand = true,
        };
        _techList = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(6),
        };
        listPanel.AddChild(new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            Children = { _techList },
        });
        view.AddChild(listPanel);

        _selected = new Label();
        LunaWindowStyle.StyleSecondary(_selected);
        view.AddChild(_selected);
        _start = new Button
        {
            Text = Loc.GetString("server-tech-reconfigurator-start"),
            MinHeight = 36,
        };
        LunaWindowStyle.ApplyCompactStyle(_start);
        _start.OnPressed += _ => StartTheft?.Invoke();
        view.AddChild(_start);
        return view;
    }

    public void UpdateState(ServerTechReconfiguratorBoundUserInterfaceState state)
    {
        _state = state;
        var target = state.HasTarget
            ? state.TargetName ?? Loc.GetString("server-tech-reconfigurator-unknown-server")
            : Loc.GetString("server-tech-reconfigurator-no-server");
        var targetColor = state.HasTarget && state.TargetPowered
            ? LunaWindowStyle.AccentGood
            : LunaWindowStyle.AccentBad;

        if (state.IsRepairMode)
        {
            _status.SetMessage(FormattedMessage.FromMarkupOrThrow(
                $"[color={LunaWindowStyle.AccentBad.ToHex()}]{target}[/color]  |  [color={LunaWindowStyle.TextSecondary.ToHex()}]{Loc.GetString("server-tech-reconfigurator-repair-status")}[/color]"));
            _selectionView.Visible = false;
            _downloadView.Visible = false;
            _puzzleView.Visible = true;
            _eject.Visible = false;
            _puzzleHelp.Text = Loc.GetString("server-tech-reconfigurator-connect-pairs");
            SetSize = new Vector2(390, 630);
            _routing.UpdateState(state.GridSize, state.Pairs, state.AcceptedRoutes);
            return;
        }

        _eject.Visible = true;
        _puzzleHelp.Text = Loc.GetString("server-tech-reconfigurator-connect-pairs");

        var disk = state.HasDisk
            ? Loc.GetString($"discovery-research-stage-{state.DiskStage.ToString().ToLowerInvariant()}")
            : Loc.GetString("discovery-research-no-disk");
        _status.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={targetColor.ToHex()}]{target}[/color]  |  [color={LunaWindowStyle.TextSecondary.ToHex()}]{disk}[/color]"));

        _selectionView.Visible = !state.PuzzleActive && !state.Downloading;
        _puzzleView.Visible = state.PuzzleActive;
        _downloadView.Visible = state.Downloading;
        _eject.Disabled = !state.HasDisk || state.PuzzleActive || state.Downloading;
        SetSize = state.PuzzleActive || state.Downloading
            ? new Vector2(390, 630)
            : new Vector2(720, 630);

        if (state.PuzzleActive)
            _routing.UpdateState(state.GridSize, state.Pairs, state.AcceptedRoutes);
        else if (state.Downloading)
            UpdateDownload(state);
        else
        {
            _selected.Text = state.SelectedTechnology == null
                ? Loc.GetString("server-tech-reconfigurator-select")
                : Loc.GetString("server-tech-reconfigurator-selected", ("technology", TechnologyName(state.SelectedTechnology)));
            _start.Disabled = !state.HasTarget ||
                              !state.TargetPowered ||
                              !state.HasDisk ||
                              state.DiskStage != ResearchDataDiskStage.Empty ||
                              state.SelectedTechnology == null;
            RebuildTechnologyList();
        }
    }

    private void UpdateDownload(ServerTechReconfiguratorBoundUserInterfaceState state)
    {
        var entry = state.Technologies.FirstOrDefault(item => item.Id == state.SelectedTechnology);
        _downloadName.Text = entry?.Name ?? state.SelectedTechnology ?? "-";
        if (!string.IsNullOrEmpty(entry?.EntityIcon) && _prototypes.HasIndex<EntityPrototype>(entry.EntityIcon))
            _downloadIcon.SetPrototype(entry.EntityIcon);
        else
            _downloadIcon.SetPrototype(null);

        var fraction = state.DownloadDuration > 0f
            ? Math.Clamp(state.DownloadProgress / state.DownloadDuration, 0f, 1f)
            : 0f;
        _downloadProgress.MinValue = 0f;
        _downloadProgress.MaxValue = 1f;
        _downloadProgress.Value = fraction;
        _downloadProgressLabel.Text = Loc.GetString(
            "server-tech-reconfigurator-download-progress",
            ("percent", (fraction * 100f).ToString("0")));
    }

    private string TechnologyName(string id)
        => _state?.Technologies.FirstOrDefault(entry => entry.Id == id)?.Name ?? id;

    private void RebuildTechnologyList()
    {
        _techList.RemoveAllChildren();
        if (_state == null)
            return;

        var any = false;
        foreach (var entry in _state.Technologies)
        {
            if (!string.IsNullOrEmpty(_query) &&
                !entry.Name.Contains(_query, StringComparison.OrdinalIgnoreCase) &&
                !entry.Id.Contains(_query, StringComparison.OrdinalIgnoreCase) &&
                !(entry.DisciplineName?.Contains(_query, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !entry.RecipeNames.Any(name => name.Contains(_query, StringComparison.OrdinalIgnoreCase)))
                continue;

            any = true;
            var selected = entry.Id == _state.SelectedTechnology;
            var button = new Button
            {
                HorizontalExpand = true,
                MinHeight = 68,
                ToggleMode = true,
                Pressed = selected,
                ToolTip = entry.Id,
            };
            LunaWindowStyle.ApplyCompactStyle(button);

            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Margin = new Thickness(6, 4),
                MouseFilter = MouseFilterMode.Ignore,
            };
            var icon = new EntityPrototypeView
            {
                Scale = new Vector2(1.5f, 1.5f),
                SetSize = new Vector2(46, 46),
                MouseFilter = MouseFilterMode.Ignore,
            };
            if (!string.IsNullOrEmpty(entry.EntityIcon) && _prototypes.HasIndex<EntityPrototype>(entry.EntityIcon))
                icon.SetPrototype(entry.EntityIcon);
            row.AddChild(icon);

            var text = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
            };
            var name = new Label
            {
                Text = entry.IsSignature ? $"★ {entry.Name}" : entry.Name,
                ClipText = true,
                MouseFilter = MouseFilterMode.Ignore,
            };
            LunaWindowStyle.StyleTiny(name);
            name.FontColorOverride = entry.DisciplineColor;
            var meta = new Label
            {
                Text = $"{entry.DisciplineName ?? entry.Discipline}  •  {entry.Cost}",
                ClipText = true,
                MouseFilter = MouseFilterMode.Ignore,
            };
            LunaWindowStyle.StyleMuted(meta);
            var recipes = new Label
            {
                Text = entry.RecipeNames.Count == 0
                    ? Loc.GetString("discovery-research-journal-no-recipes")
                    : string.Join(", ", entry.RecipeNames),
                ClipText = true,
                MouseFilter = MouseFilterMode.Ignore,
            };
            LunaWindowStyle.StyleMuted(recipes);
            text.AddChild(name);
            text.AddChild(meta);
            text.AddChild(recipes);
            row.AddChild(text);
            button.AddChild(row);

            var id = entry.Id;
            button.OnPressed += _ => SelectTechnology?.Invoke(id);
            _techList.AddChild(button);
        }

        if (!any)
        {
            var empty = new Label
            {
                Text = Loc.GetString("server-tech-reconfigurator-no-technologies"),
                HorizontalAlignment = HAlignment.Center,
            };
            LunaWindowStyle.StyleMuted(empty);
            _techList.AddChild(empty);
        }
    }
}

public sealed class ServerTechRoutingControl : Control
{
    private static readonly Color[] PairColors =
    {
        Color.FromHex("#55D6FF"),
        Color.FromHex("#FFB84D"),
        Color.FromHex("#E56BFF"),
        Color.FromHex("#65E572"),
        Color.FromHex("#FF6262"),
        Color.FromHex("#FFE066"),
        Color.FromHex("#5A8CFF"),
        Color.FromHex("#FF7BB0"),
        Color.FromHex("#53E0C2"),
        Color.FromHex("#C49BFF"),
        Color.FromHex("#FF9257"),
        Color.FromHex("#A9E34B"),
    };

    public event Action<int, List<Vector2i>>? RouteCompleted;

    private readonly IInputManager _inputManager = IoCManager.Resolve<IInputManager>();
    private int _gridSize = 12;
    private List<ServerTechRoutePair> _pairs = new();
    private Dictionary<int, List<Vector2i>> _accepted = new();
    private int? _activePair;
    private Vector2i _startCell;
    private Vector2 _cursor;
    private bool _dragging;

    public ServerTechRoutingControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    public void UpdateState(int gridSize, List<ServerTechRoutePair> pairs, List<ServerTechAcceptedRoute> routes)
    {
        _gridSize = Math.Max(2, gridSize);
        _pairs = pairs;
        _accepted = routes.ToDictionary(route => route.PairId, route => route.Cells);
        _activePair = null;
        _dragging = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;
        var local = GetMouseLocalPx();
        if (!TryHitEndpoint(local, out var pair, out var cell) || _accepted.ContainsKey(pair.Id))
            return;
        _activePair = pair.Id;
        _startCell = cell;
        _cursor = local;
        _dragging = true;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragging)
            _cursor = GetMouseLocalPx();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_dragging || _activePair == null)
            return;

        _dragging = false;
        var local = GetMouseLocalPx();
        if (TryHitEndpoint(local, out var pair, out var endCell) &&
            pair.Id == _activePair.Value &&
            endCell != _startCell)
            RouteCompleted?.Invoke(pair.Id, new List<Vector2i> { _startCell, endCell });
        _activePair = null;
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var (origin, side, cellSize) = Geometry();
        handle.DrawRect(UIBox2.FromDimensions(origin, new Vector2(side, side)), LunaWindowStyle.PanelBg);

        foreach (var (pairId, route) in _accepted)
        {
            if (route.Count != 2)
                continue;
            LunaDraw.Line(
                handle,
                CellCenter(origin, cellSize, route[0]),
                CellCenter(origin, cellSize, route[1]),
                PairColors[pairId % PairColors.Length].WithAlpha(0.88f),
                3f);
        }

        if (_dragging && _activePair is { } active)
        {
            LunaDraw.Line(
                handle,
                CellCenter(origin, cellSize, _startCell),
                _cursor,
                PairColors[active % PairColors.Length].WithAlpha(0.78f),
                3f);
        }

        foreach (var pair in _pairs)
        {
            var color = PairColors[pair.Id % PairColors.Length];
            var connected = _accepted.ContainsKey(pair.Id);
            DrawEndpoint(handle, origin, cellSize, pair.Start, color, connected);
            DrawEndpoint(handle, origin, cellSize, pair.End, color, connected);
        }
    }

    private bool TryHitEndpoint(Vector2 local, out ServerTechRoutePair pair, out Vector2i cell)
    {
        var (origin, _, cellSize) = Geometry();
        var hitRadius = MathF.Max(8f, cellSize * 0.38f);
        foreach (var candidate in _pairs)
        {
            if (Vector2.Distance(local, CellCenter(origin, cellSize, candidate.Start)) <= hitRadius)
            {
                pair = candidate;
                cell = candidate.Start;
                return true;
            }
            if (Vector2.Distance(local, CellCenter(origin, cellSize, candidate.End)) <= hitRadius)
            {
                pair = candidate;
                cell = candidate.End;
                return true;
            }
        }

        pair = null!;
        cell = default;
        return false;
    }

    private (Vector2 Origin, float Side, float CellSize) Geometry()
    {
        var side = MathF.Max(1f, MathF.Min(PixelSize.X, PixelSize.Y) - 16f);
        var origin = (PixelSize - new Vector2(side, side)) / 2f;
        return (origin, side, side / _gridSize);
    }

    private Vector2 GetMouseLocalPx()
        => GetLocalPosition(_inputManager.MouseScreenPosition);

    private static void DrawEndpoint(
        DrawingHandleScreen handle,
        Vector2 origin,
        float cellSize,
        Vector2i cell,
        Color color,
        bool connected)
    {
        var center = CellCenter(origin, cellSize, cell);
        var radius = MathF.Max(5.5f, cellSize * 0.27f);
        LunaDraw.Disk(handle, center, radius, connected ? color.WithAlpha(0.65f) : color);
        LunaDraw.Ring(handle, center, radius + 2f, color.WithAlpha(connected ? 0.9f : 0.45f), 1.5f);
    }

    private static Vector2 CellCenter(Vector2 origin, float cellSize, Vector2i cell)
        => origin + new Vector2((cell.X + 0.5f) * cellSize, (cell.Y + 0.5f) * cellSize);
}
