using System.Numerics;
using System.Text;
using Content.Lua.UIKit.Styles;
using Content.Client.Resources;
using Content.Lua.Shared.Research.Discovery;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Content.Shared.Research.Discovery;

namespace Content.Client.Lua.Research.Discovery;

[UsedImplicitly]
public sealed class ResearchComputeServerBoundUserInterface : BoundUserInterface
{
    private ResearchComputeServerWindow? _window;

    public ResearchComputeServerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ResearchComputeServerWindow>();
        _window.PowerTogglePressed += () => SendMessage(new ResearchComputeTogglePowerMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ResearchComputeServerBoundUserInterfaceState s)
            _window?.UpdateState(s);
    }
}
public sealed class ResearchComputeServerWindow : LunaWindow
{
    public event Action? PowerTogglePressed;

    private readonly Font _mono;
    private readonly Font _monoSmall;
    private readonly Font _monoTiny;
    private readonly Label _hostLabel;
    private readonly Label _uptimeHint;
    private readonly Button _powerBtn;
    private readonly Label _statusValue;
    private readonly Label _loadSpark;
    private readonly Label _powerMeter;
    private readonly Label _heatMeter;
    private readonly Label _tempValue;
    private readonly Label _gridValue;
    private readonly Label _procHeader;
    private readonly BoxContainer _procRows;

    private static readonly Color TerminalGreen = Color.FromHex("#7DDBA3");
    private static readonly Color TerminalAmber = Color.FromHex("#E8A43A");
    private static readonly Color TerminalRed = Color.FromHex("#E07070");
    private static readonly Color TerminalCyan = Color.FromHex("#5EC8E8");
    private static readonly Color TerminalDim = Color.FromHex("#4A5568");
    private const float TempMinC = -60f;
    private const float TempMaxC = 100f;

    public ResearchComputeServerWindow()
    {
        Title = Loc.GetString("research-compute-server-ui-title");
        MinSize = SetSize = new Vector2(910, 565);
        ApplyLunaChrome();

        var cache = IoCManager.Resolve<IResourceCache>();
        _mono = cache.GetFont("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf", 12);
        _monoSmall = cache.GetFont("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf", 10);
        _monoTiny = cache.GetFont("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf", 9);

        var shell = new PanelContainer
        {
            PanelOverride = LunaWindowStyle.Shell(),
            Margin = new Thickness(4, 2, 4, 4),
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(12, 10, 12, 10),
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _hostLabel = Mono(_mono, TerminalCyan);
        _uptimeHint = Mono(_monoSmall, LunaWindowStyle.TextMuted);
        _uptimeHint.Text = Loc.GetString("research-compute-server-ui-header");

        _powerBtn = new Button
        {
            Text = Loc.GetString("research-compute-server-ui-power-btn-off"),
            MinWidth = 96,
            MinHeight = 36,
            MaxHeight = 36,
            ToolTip = Loc.GetString("research-compute-server-ui-power-tooltip"),
        };
        if (_powerBtn.Label != null)
            _powerBtn.Label.FontOverride = _monoSmall;
        _powerBtn.OnPressed += _ => PowerTogglePressed?.Invoke();
        StylePowerButton(false, false);

        var hostInner = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var hostCol = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2, HorizontalExpand = true };
        hostCol.AddChild(Tag("host"));
        hostCol.AddChild(_hostLabel);
        hostCol.AddChild(_uptimeHint);
        hostInner.AddChild(hostCol);
        hostInner.AddChild(_powerBtn);
        root.AddChild(hostInner);
        root.AddChild(Divider());
        _statusValue = Mono(_mono, TerminalGreen);
        _loadSpark = Mono(_monoSmall, TerminalDim);
        root.AddChild(Tag("status"));
        root.AddChild(_statusValue);
        root.AddChild(_loadSpark);
        root.AddChild(Divider());
        _powerMeter = Mono(_monoSmall, TerminalGreen);
        _heatMeter = Mono(_monoSmall, TerminalAmber);
        _tempValue = Mono(_monoSmall, LunaWindowStyle.TextSecondary);

        var mid = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 16,
            HorizontalExpand = true,
        };

        var powerCol = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        powerCol.AddChild(Tag("power"));
        powerCol.AddChild(_powerMeter);

        var thermCol = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        thermCol.AddChild(Tag("therm"));
        thermCol.AddChild(_heatMeter);
        thermCol.AddChild(_tempValue);

        mid.AddChild(powerCol);
        mid.AddChild(thermCol);
        root.AddChild(mid);

        root.AddChild(Divider());
        _gridValue = Mono(_mono, TerminalCyan);
        root.AddChild(Tag("grid"));
        root.AddChild(_gridValue);
        root.AddChild(Divider());
        _procHeader = Mono(_monoTiny, LunaWindowStyle.TextMuted);
        _procHeader.Text = Loc.GetString("research-compute-server-ui-proc-header");
        _procRows = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 2,
            HorizontalExpand = true,
        };

        root.AddChild(Tag("proc"));
        root.AddChild(_procHeader);

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinHeight = 140,
            HScrollEnabled = false,
        };
        scroll.AddChild(_procRows);
        root.AddChild(scroll);

        shell.AddChild(root);
        ContentsContainer.AddChild(shell);
    }

    public void UpdateState(ResearchComputeServerBoundUserInterfaceState state)
    {
        Title = Loc.GetString("research-compute-server-ui-title");
        _hostLabel.Text = state.Name;

        var status = state.Broken
            ? Loc.GetString("research-compute-server-ui-status-broken")
            : state.ThermalOffline
                ? Loc.GetString("research-compute-server-ui-status-thermal")
                : !state.PowerSwitchOn
                    ? Loc.GetString("research-compute-server-ui-status-off")
                    : !state.Powered
                        ? Loc.GetString("research-compute-server-ui-status-unpowered")
                        : state.UnderLoad
                            ? Loc.GetString("research-compute-server-ui-status-load")
                            : Loc.GetString("research-compute-server-ui-status-idle");

        var statusColor = state.Broken || state.ThermalOffline
            ? TerminalRed
            : !state.PowerSwitchOn
                ? TerminalDim
                : !state.Powered
                    ? TerminalAmber
                    : state.UnderLoad
                        ? TerminalGreen
                        : TerminalCyan;

        _statusValue.Text = Loc.GetString("research-compute-server-ui-line-status", ("status", status));
        _statusValue.FontColorOverride = statusColor;

        _loadSpark.Text = state.UnderLoad
            ? Loc.GetString("research-compute-server-ui-spark-load")
            : Loc.GetString("research-compute-server-ui-spark-idle");
        _loadSpark.FontColorOverride = state.UnderLoad ? TerminalGreen : TerminalDim;

        var powerFrac = !state.PowerSwitchOn
            ? 0f
            : state.UnderLoad
                ? 1f
                : state.Powered
                    ? 0.4f
                    : 0.12f;

        _powerMeter.Text = Loc.GetString("research-compute-server-ui-line-power",
            ("watts", state.PowerLoadWatts),
            ("bar", BlockBar(powerFrac, 18)),
            ("on", state.PowerSwitchOn ? "ON" : "OFF"));
        _powerMeter.FontColorOverride = state.PowerSwitchOn
            ? (state.Powered ? TerminalGreen : TerminalAmber)
            : TerminalDim;

        var heatFrac = state.UnderLoad ? 1f : 0f;
        _heatMeter.Text = Loc.GetString("research-compute-server-ui-line-heat",
            ("watts", state.HeatWatts),
            ("bar", BlockBar(heatFrac, 18)));
        _heatMeter.FontColorOverride = ColorForHeat(state);

        var temp = state.TemperatureCelsius is { } c
            ? Loc.GetString("research-compute-server-ui-temp", ("temp", c.ToString("0.0")))
            : Loc.GetString("research-compute-server-ui-temp-vacuum");
        var tempFrac = state.TemperatureCelsius is { } tc
            ? Math.Clamp((tc - TempMinC) / (TempMaxC - TempMinC), 0f, 1f)
            : 0f;
        _tempValue.Text = Loc.GetString("research-compute-server-ui-line-temp",
            ("temp", temp),
            ("bar", BlockBar(tempFrac, 18)));
        _tempValue.FontColorOverride = ColorForAmbient(state, tempFrac);

        var gridFrac = state.LinkedOnGrid > 0
            ? Math.Clamp((float)state.WorkingOnGrid / state.LinkedOnGrid, 0f, 1f)
            : 0f;
        _gridValue.Text = Loc.GetString("research-compute-server-ui-line-grid",
            ("working", state.WorkingOnGrid),
            ("linked", state.LinkedOnGrid),
            ("bar", BlockBar(gridFrac, 20)));
        _gridValue.FontColorOverride = gridFrac > 0 ? TerminalCyan : TerminalDim;

        StylePowerButton(state.PowerSwitchOn, state.Broken);
        _powerBtn.Disabled = state.Broken;

        RebuildProcList(state.Processes);
    }

    private void RebuildProcList(List<ResearchComputeProcessEntry> processes)
    {
        _procRows.RemoveAllChildren();

        if (processes.Count == 0)
        {
            var empty = Mono(_monoSmall, TerminalDim);
            empty.Text = Loc.GetString("research-compute-server-ui-proc-empty");
            _procRows.AddChild(empty);
            return;
        }

        foreach (var proc in processes)
        {
            var kind = Loc.GetString($"research-compute-server-ui-proc-kind-{proc.Kind.ToString().ToLowerInvariant()}");
            var st = Loc.GetString($"research-compute-server-ui-proc-status-{proc.Status.ToString().ToLowerInvariant()}");
            var pct = (int)Math.Round(Math.Clamp(proc.Progress, 0f, 1f) * 100f);

            var row = Mono(_monoSmall, ColorFor(proc));
            row.Text = Loc.GetString("research-compute-server-ui-proc-row",
                ("pid", proc.Pid),
                ("kind", kind),
                ("name", proc.Name),
                ("status", st),
                ("pct", pct),
                ("bar", BlockBar(proc.Progress, 8)));
            _procRows.AddChild(row);
        }
    }

    private static Color ColorFor(ResearchComputeProcessEntry proc) => proc.Status switch
    {
        ResearchComputeProcessStatus.Run => TerminalGreen,
        ResearchComputeProcessStatus.Load => TerminalCyan,
        ResearchComputeProcessStatus.Idle => TerminalDim,
        ResearchComputeProcessStatus.Off => TerminalDim,
        ResearchComputeProcessStatus.Broken => TerminalRed,
        ResearchComputeProcessStatus.Thermal => TerminalAmber,
        _ => TerminalDim,
    };
    private static Color ColorForHeat(ResearchComputeServerBoundUserInterfaceState state)
    {
        if (state.ThermalOffline)
            return TerminalRed;
        if (state.UnderLoad || state.HeatWatts > 0)
            return TerminalAmber;
        return TerminalDim;
    }
    private static Color ColorForAmbient(ResearchComputeServerBoundUserInterfaceState state, float tempFrac)
    {
        if (state.ThermalOffline || state.TemperatureCelsius is null)
            return TerminalRed;
        if (tempFrac <= 0.12f || tempFrac >= 0.88f)
            return TerminalRed;
        if (tempFrac <= 0.28f)
            return TerminalCyan;
        if (tempFrac >= 0.72f)
            return TerminalAmber;
        return TerminalGreen;
    }

    private void StylePowerButton(bool switchOn, bool broken)
    {
        _powerBtn.Text = broken
            ? Loc.GetString("research-compute-server-ui-power-btn-broken")
            : switchOn
                ? Loc.GetString("research-compute-server-ui-power-btn-on")
                : Loc.GetString("research-compute-server-ui-power-btn-off");

        if (_powerBtn.Label != null)
        {
            _powerBtn.Label.FontOverride = _monoSmall;
            _powerBtn.Label.FontColorOverride = broken
                ? TerminalRed
                : switchOn
                    ? TerminalGreen
                    : TerminalDim;
        }

        _powerBtn.ToolTip = broken
            ? Loc.GetString("research-compute-server-ui-power-broken")
            : switchOn
                ? Loc.GetString("research-compute-server-ui-power-on")
                : Loc.GetString("research-compute-server-ui-power-off");
    }

    private Label Tag(string tag) => new()
    {
        Text = tag,
        FontOverride = _monoTiny,
        FontColorOverride = LunaWindowStyle.TextMuted,
    };

    private static PanelContainer Divider()
    {
        var div = new PanelContainer
        {
            MinHeight = 1,
            MaxHeight = 1,
            HorizontalExpand = true,
        };
        LunaWindowStyle.StyleDivider(div);
        return div;
    }

    private static Label Mono(Font font, Color color) => new()
    {
        FontOverride = font,
        FontColorOverride = color,
        HorizontalExpand = true,
    };

    private static string BlockBar(float fraction, int width)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        var filled = (int)Math.Round(fraction * width);
        var sb = new StringBuilder(width);
        for (var i = 0; i < width; i++)
            sb.Append(i < filled ? '#' : '.');
        return sb.ToString();
    }
}
