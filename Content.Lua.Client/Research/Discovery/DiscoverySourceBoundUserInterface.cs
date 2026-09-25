using System.Linq;
using System.Numerics;
using Content.Client.Lua.Research;
using Content.Lua.UIKit.Styles;
using Content.Client.PhysicsSystem.Controllers;
using Content.Lua.Shared.Research.Discovery;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Content.Shared.Research.Discovery;

namespace Content.Client.Lua.Research.Discovery;

[UsedImplicitly]
public sealed class DiscoverySourceBoundUserInterface : BoundUserInterface
{
    private DiscoverySourceWindow? _window;

    public DiscoverySourceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DiscoverySourceWindow>();
        _window.TunePressed += dir => SendMessage(new DiscoveryTuneSignalMessage(dir));
        _window.SelectNodePressed += id => SendMessage(new DiscoverySelectNodeMessage(id));
        _window.ScanPressed += () => SendMessage(new DiscoveryStartScanMessage());
        _window.AutoPressed += enabled => SendMessage(new DiscoverySetAutoMessage(enabled));
        _window.EjectPressed += () => SendMessage(new DiscoveryEjectDiskMessage());
        _window.ServerPressed += () => SendMessage(new Content.Shared.Research.Components.ConsoleServerSelectionMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DiscoverySourceBoundUserInterfaceState s)
            _window?.UpdateState(s, EntMan);
    }
}

public sealed class DiscoverySourceWindow : LunaWindow
{
    public event Action<Vector2>? TunePressed;
    public event Action<string>? SelectNodePressed;
    public event Action? ScanPressed;
    public event Action<bool>? AutoPressed;
    public event Action? EjectPressed;
    public event Action? ServerPressed;

    private readonly RichTextLabel _header;
    private readonly RichTextLabel _uniqueDataLabel;
    private readonly RichTextLabel _statusLabel;
    private readonly RichTextLabel _diskLabel;
    private readonly RichTextLabel _previewLabel;
    private readonly PanelContainer _serverStatusDot;
    private readonly ProgressBar _progress;
    private readonly Label _progressLabel;
    private readonly Button _scan;
    private readonly Button _auto;
    private readonly Button _eject;
    private readonly SpriteView _artifactView;
    private readonly DiscoverySignalHuntControl _signalHunt;
    private readonly Label _noSampleLabel;
    private readonly BoxContainer _nodeLevelTabs;
    private readonly BoxContainer _nodeList;
    private readonly PanelContainer _nodesFrame;
    private bool _progressStyledScanning;
    private bool _autoRunning;
    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private int _selectedDepth;
    private List<DiscoveryArtifactNodeEntry> _nodes = new();
    private bool _canTune;
    private Vector2 _lastSentInput;
    private bool _discoveryInputActive;
    private readonly IEntityManager _entMan = IoCManager.Resolve<IEntityManager>();

    private const float ArtifactBox = 300f;

    public DiscoverySourceWindow()
    {
        Title = Loc.GetString("discovery-research-source-title-anomaly");
        MinSize = SetSize = new Vector2(1100, 720);
        ApplyLunaChrome();
        OnOpen += EnableDiscoveryMovementCapture;
        OnClose += DisableDiscoveryMovementCapture;

        var shell = new PanelContainer
        {
            PanelOverride = LunaWindowStyle.Shell(),
            Margin = new Thickness(4, 2, 4, 4),
        };
        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(10, 8, 10, 10),
        };

        var left = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var topRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            VerticalExpand = true,
            SizeFlagsStretchRatio = 1.4f,
        };

        var sampleFrame = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinSize = new Vector2(320, 320),
            PanelOverride = LunaWindowStyle.Box(LunaWindowStyle.ContentBg, LunaWindowStyle.Accent),
        };

        var huntHost = new LayoutContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            SetSize = new Vector2(ArtifactBox, ArtifactBox),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };

        _artifactView = new SpriteView
        {
            Scale = new Vector2(10f, 10f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            OverrideDirection = Direction.South,
            SetSize = new Vector2(ArtifactBox, ArtifactBox),
        };
        LayoutContainer.SetAnchorPreset(_artifactView, LayoutContainer.LayoutPreset.Wide);

        _signalHunt = new DiscoverySignalHuntControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            SetSize = new Vector2(ArtifactBox, ArtifactBox),
            RectClipContent = true,
        };
        LayoutContainer.SetAnchorPreset(_signalHunt, LayoutContainer.LayoutPreset.Wide);

        _noSampleLabel = new Label
        {
            Text = Loc.GetString("discovery-research-need-analyzer-artifact"),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
            MaxWidth = ArtifactBox - 20,
        };
        LunaWindowStyle.StyleMuted(_noSampleLabel);
        LayoutContainer.SetAnchorPreset(_noSampleLabel, LayoutContainer.LayoutPreset.Wide);

        huntHost.AddChild(_artifactView);
        huntHost.AddChild(_signalHunt);
        huntHost.AddChild(_noSampleLabel);
        sampleFrame.AddChild(huntHost);
        topRow.AddChild(sampleFrame);
        _nodesFrame = new PanelContainer
        {
            SetWidth = 340,
            VerticalExpand = true,
            PanelOverride = LunaWindowStyle.Box(LunaWindowStyle.ContentBg, LunaWindowStyle.PanelBorder),
            RectClipContent = true,
            Visible = false,
        };
        var nodesCol = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(8),
            RectClipContent = true,
        };
        var nodesHeader = new Label { Text = Loc.GetString("discovery-research-nodes-header"), MaxWidth = 300 };
        LunaWindowStyle.StyleSecondary(nodesHeader);
        _nodeLevelTabs = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 4 };
        _nodeList = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };
        nodesCol.AddChild(nodesHeader);
        nodesCol.AddChild(_nodeLevelTabs);
        nodesCol.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _nodeList },
        });
        _nodesFrame.AddChild(nodesCol);
        topRow.AddChild(_nodesFrame);
        topRow.RemoveChild(_nodesFrame);
        topRow.RemoveChild(sampleFrame);
        topRow.AddChild(_nodesFrame);
        topRow.AddChild(sampleFrame);

        left.AddChild(topRow);


        _progress = new ProgressBar();
        LunaWindowStyle.StyleProgressCooldown(_progress, 16f);
        _progressLabel = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleSecondary(_progressLabel);
        left.AddChild(_progress);
        left.AddChild(_progressLabel);

        var buttons = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _scan = new Button
        {
            Text = Loc.GetString("discovery-research-scan-button"),
            HorizontalExpand = true,
            SizeFlagsStretchRatio = 1.15f,
            MinHeight = 34,
        };
        _scan.OnPressed += _ => ScanPressed?.Invoke();
        _auto = new Button
        {
            Text = Loc.GetString("discovery-research-auto-button"),
            MinWidth = 88,
            MinHeight = 34,
        };
        _auto.OnPressed += _ => AutoPressed?.Invoke(!_autoRunning);
        _eject = new Button { Text = Loc.GetString("discovery-research-eject-button"), HorizontalExpand = true, MinHeight = 34 };
        _eject.OnPressed += _ => EjectPressed?.Invoke();
        LunaWindowStyle.ApplyCompactStyle(_scan);
        LunaWindowStyle.ApplyCompactStyle(_auto);
        LunaWindowStyle.ApplyCompactStyle(_eject);
        buttons.AddChild(_scan);
        buttons.AddChild(_auto);
        buttons.AddChild(_eject);
        left.AddChild(buttons);

        var right = new PanelContainer
        {
            SetWidth = 300,
            VerticalExpand = true,
            PanelOverride = LunaWindowStyle.Panel(),
            RectClipContent = true,
        };
        var rightCol = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10),
            RectClipContent = true,
        };

        var hint = new Label
        {
            Text = Loc.GetString("discovery-research-signal-hint"),
            MaxWidth = 270,
        };
        LunaWindowStyle.StyleSecondary(hint);

        _header = WrapLabel();
        _uniqueDataLabel = WrapLabel();
        _statusLabel = WrapLabel();
        _diskLabel = WrapLabel();
        _previewLabel = WrapLabel();

        var (server, serverDot) = ResearchServerStatusDot.CreateSelectButton(
            Loc.GetString("discovery-research-server-button"),
            minHeight: 32);
        server.HorizontalExpand = true;
        server.OnPressed += _ => ServerPressed?.Invoke();
        _serverStatusDot = serverDot;

        rightCol.AddChild(hint);
        rightCol.AddChild(_header);
        rightCol.AddChild(_uniqueDataLabel);
        rightCol.AddChild(_statusLabel);
        rightCol.AddChild(_diskLabel);
        rightCol.AddChild(_previewLabel);
        rightCol.AddChild(new Control { VerticalExpand = true });
        rightCol.AddChild(server);
        right.AddChild(rightCol);

        root.AddChild(left);
        root.AddChild(right);
        shell.AddChild(root);
        ContentsContainer.AddChild(shell);

        OnKeyBindDown += BlockMoveWhileOpen;
        OnKeyBindUp += BlockMoveWhileOpen;
        OnClose += () => SendAimInput(Vector2.Zero);
    }

    private readonly IInputManager _input = IoCManager.Resolve<IInputManager>();

    private void EnableDiscoveryMovementCapture()
    {
        if (_discoveryInputActive)
            return;

        _discoveryInputActive = true;
        _entMan.System<MoverController>().ClearLocalMovement();
        SendAimInput(Vector2.Zero);
    }

    private void DisableDiscoveryMovementCapture()
    {
        if (!_discoveryInputActive)
            return;

        _discoveryInputActive = false;
        SendAimInput(Vector2.Zero);
        _entMan.System<MoverController>().ClearLocalMovement();
        _entMan.System<InputSystem>().SetEntityContextActive();
    }

    private void BlockMoveWhileOpen(GUIBoundKeyEventArgs args)
    {
        if (!IsOpen)
            return;

        if (args.Function == EngineKeyFunctions.MoveUp ||
            args.Function == EngineKeyFunctions.MoveDown ||
            args.Function == EngineKeyFunctions.MoveLeft ||
            args.Function == EngineKeyFunctions.MoveRight)
            args.Handle();
    }

    private static RichTextLabel WrapLabel()
    {
        return new RichTextLabel
        {
            MaxWidth = 270,
            HorizontalExpand = true,
        };
    }

    public void UpdateState(DiscoverySourceBoundUserInterfaceState state, IEntityManager entMan)
    {
        Title = Loc.GetString(state.ArtifactPadMode
            ? "discovery-research-source-title-artifact"
            : "discovery-research-source-title-anomaly");

        ResearchServerStatusDot.Set(_serverStatusDot, state.HasServer);

        _signalHunt.AimNormalized = state.AimNormalized;
        _signalHunt.SignalNormalized = state.SignalNormalized;
        _signalHunt.Locked = state.SignalLocked;

        EntityUid? sourceUid = null;
        if (state.SourceEntity is { } net && entMan.TryGetEntity(net, out var resolved))
            sourceUid = resolved;

        if (sourceUid != null)
        {
            _artifactView.SetEntity(sourceUid);
            _artifactView.Visible = true;
            _signalHunt.HasSample = true;
            _signalHunt.Visible = true;
            _noSampleLabel.Visible = false;
        }
        else
        {
            _artifactView.SetEntity(null);
            _artifactView.Visible = false;
            _signalHunt.HasSample = false;
            _signalHunt.Visible = false;
            _noSampleLabel.Visible = true;
            if (state.ArtifactPadMode)
            {
                _noSampleLabel.Text = !state.HasAnalyzer
                    ? Loc.GetString("discovery-research-need-analyzer-link")
                    : Loc.GetString("discovery-research-need-analyzer-artifact");
            }
            else
            {
                _noSampleLabel.Text = !state.HasAnalyzer
                    ? Loc.GetString("discovery-research-need-vessel-link")
                    : Loc.GetString("discovery-research-need-vessel-anomaly");
            }
        }

        var srcName = state.HasSource ? state.SourceName ?? "-" : "-";
        _header.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={LunaWindowStyle.Accent.ToHex()}]{Loc.GetString("discovery-research-source-header", ("source", srcName))}[/color]"));

        if (state.HasSource && state.UniqueDataPercent >= 0)
        {
            _uniqueDataLabel.Visible = true;
            var dataColor = state.UniqueDataPercent > 0 ? LunaWindowStyle.AccentGood : LunaWindowStyle.TextMuted;
            _uniqueDataLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
                $"[color={dataColor.ToHex()}]{Loc.GetString("discovery-research-unique-data", ("percent", state.UniqueDataPercent))}[/color]"));
        }
        else
            _uniqueDataLabel.Visible = false;

        _statusLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={LunaWindowStyle.TextSecondary.ToHex()}]{Loc.GetString("discovery-research-source-compute", ("working", state.WorkingComputeCount), ("linked", state.LinkedComputeCount), ("points", state.ServerPoints), ("unlocked", state.UnlockedCount))}[/color]\n" +
            $"[color={(state.SignalLocked ? LunaWindowStyle.AccentGood.ToHex() : LunaWindowStyle.AccentWarn.ToHex())}]{(state.SignalLocked ? Loc.GetString("discovery-research-signal-aligned") : Loc.GetString("discovery-research-signal-hint"))}[/color]"));

        var diskColor = state.DiskStage switch
        {
            ResearchDataDiskStage.Empty => LunaWindowStyle.TextMuted,
            ResearchDataDiskStage.Raw => LunaWindowStyle.AccentWarn,
            ResearchDataDiskStage.Decoded => LunaWindowStyle.AccentGood,
            _ => LunaWindowStyle.TextMuted,
        };
        var diskStageLoc = Loc.GetString($"discovery-research-disk-stage-{state.DiskStage.ToString().ToLowerInvariant()}");
        _diskLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
            $"[color={diskColor.ToHex()}]{Loc.GetString("discovery-research-disk-stage", ("stage", diskStageLoc))}[/color]" +
            (state.HasDisk ? "" : $"  [color={LunaWindowStyle.AccentBad.ToHex()}]{Loc.GetString("discovery-research-no-disk")}[/color]")));

        if (state.DiskStage == ResearchDataDiskStage.Raw)
        {
            _previewLabel.Visible = true;
            _previewLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
                $"[color={LunaWindowStyle.AccentWarn.ToHex()}]{Loc.GetString("discovery-research-raw-preview", ("discipline", DiscoveryUiLoc.DisciplineName(state.PreviewDiscipline, _prototypes)), ("cost", state.PreviewCostEstimate), ("risk", DiscoveryUiLoc.RiskName(state.PreviewRisk)))}[/color]"));
        }
        else
            _previewLabel.Visible = false;

        RebuildNodes(state);

        if (state.Scanning != _progressStyledScanning)
        {
            _progressStyledScanning = state.Scanning;
            if (state.Scanning)
                LunaWindowStyle.StyleProgressGood(_progress, 16f);
            else
                LunaWindowStyle.StyleProgressCooldown(_progress, 16f);
        }

        var fraction = state.Scanning && state.ScanDuration > 0f
            ? Math.Clamp(state.ScanProgress / state.ScanDuration, 0f, 1f)
            : 0f;
        _progress.MinValue = 0f;
        _progress.MaxValue = 1f;
        _progress.Value = fraction;

        if (state.Scanning)
        {
            var progressKey = state.AutoScanning
                ? "discovery-research-auto-progress"
                : "discovery-research-scan-progress";
            _progressLabel.Text = Loc.GetString(progressKey, ("percent", (fraction * 100f).ToString("0")));
        }
        else if (state.SignalLocked)
            _progressLabel.Text = Loc.GetString("discovery-research-signal-aligned");
        else
            _progressLabel.Text = Loc.GetString("discovery-research-signal-hint");

        _canTune = state.HasSource && !state.Scanning;
        if (!_canTune && _lastSentInput != Vector2.Zero)
            SendAimInput(Vector2.Zero);

        var hasEmptyDisk = state.HasDisk && state.DiskStage == ResearchDataDiskStage.Empty;
        var needsNode = state.ArtifactPadMode && state.HasSource && string.IsNullOrEmpty(state.SelectedNodeId);
        var canAuto = state.HasSource
                      && hasEmptyDisk
                      && (!state.ArtifactPadMode || !string.IsNullOrEmpty(state.SelectedNodeId));
        _scan.Disabled = state.Scanning || !state.SignalLocked || !state.HasSource || !hasEmptyDisk || needsNode;
        _autoRunning = state.AutoScanning;
        _auto.Disabled = state.AutoScanning ? false : state.Scanning || !canAuto;
        _auto.Text = Loc.GetString(state.AutoScanning
            ? "discovery-research-auto-stop"
            : "discovery-research-auto-button");
        _eject.Disabled = state.Scanning;
    }


    protected override void FrameUpdate(Robust.Shared.Timing.FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_canTune || !IsOpen)
        {
            if (_lastSentInput != Vector2.Zero)
                SendAimInput(Vector2.Zero);
            return;
        }
        var input = new Vector2(
            (_input.IsKeyDown(Keyboard.Key.D) ? 1f : 0f) - (_input.IsKeyDown(Keyboard.Key.A) ? 1f : 0f),
            (_input.IsKeyDown(Keyboard.Key.S) ? 1f : 0f) - (_input.IsKeyDown(Keyboard.Key.W) ? 1f : 0f));
        if (input != _lastSentInput)
            SendAimInput(input);
    }

    private void SendAimInput(Vector2 input)
    {
        if (input == _lastSentInput)
            return;
        _lastSentInput = input;
        TunePressed?.Invoke(input);
    }

    private void RebuildNodes(DiscoverySourceBoundUserInterfaceState state)
    {
        _nodes = state.ArtifactNodes.ToList();
        var show = state.ArtifactPadMode && _nodes.Count > 0;
        _nodesFrame.Visible = show;
        if (!show)
        {
            _nodeLevelTabs.RemoveAllChildren();
            _nodeList.RemoveAllChildren();
            return;
        }
        var maxUnlocked = _nodes.Where(n => !n.Locked).Select(n => n.Depth).DefaultIfEmpty(-1).Max();
        var visibleMax = Math.Max(0, maxUnlocked + 1);
        var depths = _nodes
            .Select(n => n.Depth)
            .Where(d => d <= visibleMax)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (depths.Count == 0)
            depths.Add(0);

        if (!depths.Contains(_selectedDepth))
            _selectedDepth = depths[0];

        _nodeLevelTabs.RemoveAllChildren();
        foreach (var depth in depths)
        {
            var d = depth;
            var btn = new Button
            {
                Text = Loc.GetString("discovery-research-node-level", ("level", d)),
                ToggleMode = true,
                Pressed = d == _selectedDepth,
                MinHeight = 28,
                MinWidth = 52,
            };
            LunaWindowStyle.ApplyCompactStyle(btn);
            btn.OnPressed += _ =>
            {
                _selectedDepth = d;
                RebuildNodeCards();
                foreach (var child in _nodeLevelTabs.Children)
                {
                    if (child is Button other)
                        other.Pressed = other == btn;
                }
            };
            _nodeLevelTabs.AddChild(btn);
        }

        RebuildNodeCards();
    }

    private void RebuildNodeCards()
    {
        _nodeList.RemoveAllChildren();
        foreach (var node in _nodes.Where(n => n.Depth == _selectedDepth).OrderBy(n => n.Id))
        {
            var color = node.Selected
                ? LunaWindowStyle.Accent
                : node.Active
                    ? LunaWindowStyle.AccentGood
                    : node.CanUnlock
                        ? LunaWindowStyle.AccentWarn
                        : node.Locked
                            ? LunaWindowStyle.TextMuted
                            : LunaWindowStyle.Accent;
            var status = node.Selected
                ? Loc.GetString("discovery-research-node-selected")
                : node.Active
                    ? Loc.GetString("discovery-research-node-active")
                    : node.CanUnlock
                        ? Loc.GetString("discovery-research-node-ready")
                        : node.Locked
                            ? Loc.GetString("discovery-research-node-locked")
                            : Loc.GetString("discovery-research-node-unlocked");

            var points = Loc.GetString("discovery-research-node-research-value", ("points", node.ResearchValue));

            var border = node.Selected ? LunaWindowStyle.Accent : LunaWindowStyle.PanelBorder;
            var card = new PanelContainer
            {
                PanelOverride = LunaWindowStyle.Box(LunaWindowStyle.PanelBg, border),
                Margin = new Thickness(0, 1),
                HorizontalExpand = true,
                MouseFilter = MouseFilterMode.Stop,
            };
            var capturedId = node.Id;
            card.OnKeyBindDown += args =>
            {
                if (args.Function != EngineKeyFunctions.UIClick)
                    return;
                SelectNodePressed?.Invoke(capturedId);
                args.Handle();
            };

            var col = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 2,
                Margin = new Thickness(6, 4),
                MouseFilter = MouseFilterMode.Ignore,
            };
            var head = new RichTextLabel { MaxWidth = 300, MouseFilter = MouseFilterMode.Ignore };
            head.SetMessage(FormattedMessage.FromMarkupOrThrow(
                $"[color={color.ToHex()}]#{node.Id}[/color]  [color={LunaWindowStyle.TextSecondary.ToHex()}]{status} | {points}[/color]"));
            col.AddChild(head);

            var effectText = node.Locked
                ? Loc.GetString("discovery-research-node-effect-unknown")
                : string.IsNullOrWhiteSpace(node.Effect)
                    ? Loc.GetString("discovery-research-node-effect-unknown")
                    : node.Effect;
            var effect = new Label
            {
                Text = effectText,
                MaxWidth = 300,
                FontColorOverride = node.Locked ? LunaWindowStyle.TextMuted : LunaWindowStyle.TextPrimary,
                MouseFilter = MouseFilterMode.Ignore,
            };
            LunaWindowStyle.StyleTiny(effect);
            col.AddChild(effect);

            card.AddChild(col);
            _nodeList.AddChild(card);
        }
    }
}
