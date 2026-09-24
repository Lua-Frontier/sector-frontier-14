using System.Numerics;
using Content.Lua.UIKit.Styles;
using Content.Lua.Shared.Anomaly;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Lua.Anomaly;

[UsedImplicitly]
public sealed class AnomalySynchronizerConsoleBoundUserInterface : BoundUserInterface
{
    private AnomalySynchronizerConsoleWindow? _window;

    public AnomalySynchronizerConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AnomalySynchronizerConsoleWindow>();
        _window.ConnectPressed += () => SendMessage(new AnomalySyncConnectMessage());
        _window.DisconnectPressed += () => SendMessage(new AnomalySyncDisconnectMessage());
        _window.CompressPressed += () => SendMessage(new AnomalySyncCompressMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is AnomalySynchronizerConsoleState s)
            _window?.UpdateState(s, EntMan);
    }
}

public sealed class AnomalySynchronizerConsoleWindow : LunaWindow
{
    public event Action? ConnectPressed;
    public event Action? DisconnectPressed;
    public event Action? CompressPressed;

    private readonly PanelContainer _previewFrame;
    private readonly EntityPrototypeView _syncView;
    private readonly SpriteView _anomalyView;
    private readonly EntityPrototypeView _compressFx;
    private readonly Label _previewLabel;
    private readonly RichTextLabel _linkStatus;
    private readonly RichTextLabel _scanner;
    private readonly ProgressBar _compressProgress;
    private readonly Label _compressLabel;
    private readonly ProgressBar _batteryBar;
    private readonly BoxContainer _batteryTicks;
    private readonly Label _batteryLabel;
    private readonly Button _connect;
    private readonly Button _disconnect;
    private readonly Button _compress;

    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private string _scannerBase = string.Empty;
    private TimeSpan? _nextPulse;
    private bool _compressing;
    private float _compressPulse;
    private const float PreviewBox = 280f;
    private const int BatterySegments = 10;

    public AnomalySynchronizerConsoleWindow()
    {
        Title = Loc.GetString("anomaly-sync-console-title");
        MinSize = SetSize = new Vector2(860, 600);
        ApplyLunaChrome();

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
            SeparationOverride = 8,
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        _previewFrame = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinSize = new Vector2(PreviewBox, PreviewBox),
            PanelOverride = LunaWindowStyle.Box(LunaWindowStyle.ContentBg, LunaWindowStyle.Accent),
        };

        var previewHost = new LayoutContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            SetSize = new Vector2(PreviewBox, PreviewBox),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };

        _syncView = new EntityPrototypeView
        {
            Scale = new Vector2(8f, 8f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            OverrideDirection = Direction.South,
            SetSize = new Vector2(PreviewBox, PreviewBox),
            Visible = false,
        };
        LayoutContainer.SetAnchorPreset(_syncView, LayoutContainer.LayoutPreset.Wide);

        _anomalyView = new SpriteView
        {
            Scale = new Vector2(8f, 8f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            OverrideDirection = Direction.South,
            SetSize = new Vector2(PreviewBox, PreviewBox),
            Visible = false,
        };
        LayoutContainer.SetAnchorPreset(_anomalyView, LayoutContainer.LayoutPreset.Wide);

        _compressFx = new EntityPrototypeView
        {
            Scale = new Vector2(6f, 6f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            OverrideDirection = Direction.South,
            SetSize = new Vector2(PreviewBox, PreviewBox),
            Visible = false,
        };
        LayoutContainer.SetAnchorPreset(_compressFx, LayoutContainer.LayoutPreset.Wide);

        _previewLabel = new Label
        {
            Text = Loc.GetString("anomaly-sync-console-unlinked"),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
            MaxWidth = PreviewBox - 24,
        };
        LunaWindowStyle.StyleMuted(_previewLabel);
        LayoutContainer.SetAnchorPreset(_previewLabel, LayoutContainer.LayoutPreset.Wide);

        previewHost.AddChild(_syncView);
        previewHost.AddChild(_anomalyView);
        previewHost.AddChild(_compressFx);
        previewHost.AddChild(_previewLabel);
        _previewFrame.AddChild(previewHost);
        left.AddChild(_previewFrame);

        var batteryHeader = new Label { Text = Loc.GetString("anomaly-sync-console-battery") };
        LunaWindowStyle.StyleSecondary(batteryHeader);
        _batteryBar = new ProgressBar { MinValue = 0, MaxValue = 1, HorizontalExpand = true };
        LunaWindowStyle.StyleProgressConfirm(_batteryBar, 14f);
        _batteryTicks = BuildBatteryTicks();
        var batteryStack = new LayoutContainer
        {
            HorizontalExpand = true,
            SetHeight = 14f,
        };
        LayoutContainer.SetAnchorPreset(_batteryBar, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorPreset(_batteryTicks, LayoutContainer.LayoutPreset.Wide);
        batteryStack.AddChild(_batteryBar);
        batteryStack.AddChild(_batteryTicks);
        _batteryLabel = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleTiny(_batteryLabel);

        _compressProgress = new ProgressBar { MinValue = 0, MaxValue = 1, HorizontalExpand = true };
        LunaWindowStyle.StyleProgressGood(_compressProgress, 16f);
        _compressLabel = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleSecondary(_compressLabel);

        var buttons = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _connect = MakeButton("anomaly-sync-console-connect", () => ConnectPressed?.Invoke());
        _disconnect = MakeButton("anomaly-sync-console-disconnect", () => DisconnectPressed?.Invoke());
        _compress = MakeButton("anomaly-sync-console-compress", () => CompressPressed?.Invoke());
        buttons.AddChild(_connect);
        buttons.AddChild(_disconnect);
        buttons.AddChild(_compress);

        left.AddChild(batteryHeader);
        left.AddChild(batteryStack);
        left.AddChild(_batteryLabel);
        left.AddChild(_compressProgress);
        left.AddChild(_compressLabel);
        left.AddChild(buttons);

        var right = new PanelContainer
        {
            SetWidth = 340,
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

        _linkStatus = new RichTextLabel { MaxWidth = 310, HorizontalExpand = true };
        _scanner = new RichTextLabel { MaxWidth = 310, HorizontalExpand = true, VerticalExpand = true };
        LunaWindowStyle.StyleCompactRich(_linkStatus);
        LunaWindowStyle.StyleCompactRich(_scanner);

        rightCol.AddChild(_linkStatus);
        rightCol.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _scanner },
        });
        right.AddChild(rightCol);

        root.AddChild(left);
        root.AddChild(right);
        shell.AddChild(root);
        ContentsContainer.AddChild(shell);
    }

    public void UpdateState(AnomalySynchronizerConsoleState state, IEntityManager entMan)
    {
        _compressing = state.Compressing;

        if (state.Linked)
        {
            _syncView.SetPrototype("MachineAnomalySynchronizer");
            _syncView.Visible = true;
            _previewLabel.Visible = !state.HasAnomaly;
            _previewLabel.Text = state.HasAnomaly
                ? string.Empty
                : Loc.GetString("anomaly-sync-console-no-anomaly");
        }
        else
        {
            _syncView.SetPrototype(null);
            _syncView.Visible = false;
            _previewLabel.Visible = true;
            _previewLabel.Text = Loc.GetString("anomaly-sync-console-unlinked");
        }

        if (state.HasAnomaly && state.AnomalyEntity is { } net && entMan.TryGetEntity(net, out var anomalyUid))
        {
            _anomalyView.SetEntity(anomalyUid);
            _anomalyView.Visible = true;
            _previewLabel.Visible = false;
        }
        else
        {
            _anomalyView.SetEntity(null);
            _anomalyView.Visible = false;
        }

        if (state.Compressing)
        {
            _compressFx.SetPrototype("EffectDesynchronizer");
            _compressFx.Visible = true;
            _previewFrame.PanelOverride = LunaWindowStyle.Box(LunaWindowStyle.ContentBg, LunaWindowStyle.AccentWarn);
        }
        else
        {
            _compressFx.SetPrototype(null);
            _compressFx.Visible = false;
            _previewFrame.PanelOverride = LunaWindowStyle.Box(LunaWindowStyle.ContentBg, LunaWindowStyle.Accent);
            _anomalyView.Scale = new Vector2(8f, 8f);
            _syncView.Scale = new Vector2(8f, 8f);
        }

        var link = state.Linked
            ? Loc.GetString("anomaly-sync-console-linked", ("name", state.SynchronizerName))
            : Loc.GetString("anomaly-sync-console-unlinked");
        var phase = Loc.GetString($"anomaly-sync-console-phase-{state.Phase}");
        _linkStatus.SetMessage(FormattedMessage.FromMarkupOrThrow(LunaWindowStyle.CompactMarkup(
            $"[color={LunaWindowStyle.Accent.ToHex()}]{link}[/color]\n" +
            $"[color={LunaWindowStyle.TextSecondary.ToHex()}]{phase}[/color]\n" +
            Loc.GetString("anomaly-sync-console-meters",
                ("severity", state.SeverityPercent),
                ("stability", state.StabilityPercent),
                ("health", state.HealthPercent)))));

        _scannerBase = state.HasAnomaly ? state.ScannerText : Loc.GetString("anomaly-sync-console-no-anomaly");
        _nextPulse = state.HasAnomaly ? state.NextPulseTime : null;
        RefreshScannerText();

        UpdateBattery(state);

        var fraction = state.Compressing && state.CompressDuration > 0f
            ? Math.Clamp(state.CompressProgress / state.CompressDuration, 0f, 1f)
            : 0f;
        _compressProgress.Value = fraction;
        _compressLabel.Text = state.Compressing
            ? Loc.GetString("anomaly-sync-console-compressing", ("percent", (fraction * 100f).ToString("0")))
            : Loc.GetString("anomaly-sync-console-compress-idle");

        _connect.Disabled = !state.Linked || !state.Powered || state.HasAnomaly || state.Compressing;
        _disconnect.Disabled = !state.HasAnomaly || state.Compressing;
        _compress.Disabled = !state.CanCompress;
        _compress.Text = state.Compressing
            ? Loc.GetString("anomaly-sync-console-compressing", ("percent", (fraction * 100f).ToString("0")))
            : Loc.GetString("anomaly-sync-console-compress");
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_nextPulse != null)
            RefreshScannerText();

        if (!_compressing)
            return;

        _compressPulse += args.DeltaSeconds * 4f;
        var wave = 0.5f + 0.5f * MathF.Sin(_compressPulse);
        var scale = 7.2f + wave * 1.6f;
        _anomalyView.Scale = new Vector2(scale, scale);
        _compressFx.Scale = new Vector2(5f + wave * 2f, 5f + wave * 2f);
        var border = Color.InterpolateBetween(LunaWindowStyle.Accent, LunaWindowStyle.AccentWarn, wave);
        _previewFrame.PanelOverride = LunaWindowStyle.Box(LunaWindowStyle.ContentBg, border);
    }

    private void UpdateBattery(AnomalySynchronizerConsoleState state)
    {
        if (!state.HasBattery)
        {
            _batteryBar.Value = 0f;
            _batteryLabel.Text = Loc.GetString("anomaly-sync-console-battery-empty");
            LunaWindowStyle.StyleProgressBar(_batteryBar, LunaWindowStyle.AccentBad, 14f);
            return;
        }

        var fraction = Math.Clamp(state.BatteryPercent / 100f, 0f, 1f);
        _batteryBar.Value = fraction;
        var fill = fraction > 0.35f
            ? LunaWindowStyle.ProgressConfirm
            : fraction > 0.15f
                ? LunaWindowStyle.ProgressCooldown
                : LunaWindowStyle.AccentBad;
        LunaWindowStyle.StyleProgressBar(_batteryBar, fill, 14f);

        _batteryLabel.Text = state.BatteryCharging
            ? Loc.GetString("anomaly-sync-console-battery-charging", ("percent", state.BatteryPercent))
            : Loc.GetString("anomaly-sync-console-battery-level", ("percent", state.BatteryPercent));
    }

    private void RefreshScannerText()
    {
        var msg = FormattedMessage.FromMarkupOrThrow(LunaWindowStyle.CompactMarkup(_scannerBase));
        if (_nextPulse != null)
        {
            msg.PushNewline();
            msg.PushNewline();
            var time = _nextPulse.Value - _timing.CurTime;
            if (time < TimeSpan.Zero)
                time = TimeSpan.Zero;
            var timestring = $"{time.Minutes:00}:{time.Seconds:00}";
            msg.AddMarkupOrThrow(LunaWindowStyle.CompactMarkup(
                Loc.GetString("anomaly-scanner-pulse-timer", ("time", timestring))));
        }

        _scanner.SetMessage(msg);
        LunaWindowStyle.StyleCompactRich(_scanner);
    }

    private static BoxContainer BuildBatteryTicks()
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        for (var i = 0; i < BatterySegments; i++)
        {
            row.AddChild(new Control { HorizontalExpand = true });
            if (i >= BatterySegments - 1)
                continue;

            row.AddChild(new PanelContainer
            {
                MinWidth = 1,
                MaxWidth = 1,
                VerticalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = LunaWindowStyle.PanelBorder,
                },
            });
        }

        return row;
    }

    private static Button MakeButton(string loc, Action action)
    {
        var button = new Button
        {
            Text = Loc.GetString(loc),
            HorizontalExpand = true,
            MinHeight = 32,
        };
        LunaWindowStyle.ApplyCompactStyle(button);
        button.OnPressed += _ => action();
        return button;
    }
}
