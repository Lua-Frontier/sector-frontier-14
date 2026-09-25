using System.Numerics;
using Content.Client.Lua.Research;
using Content.Lua.UIKit.Styles;
using Content.Lua.Shared.Research.Discovery;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Content.Shared.Research.Discovery;

namespace Content.Client.Lua.Research.Discovery;

[UsedImplicitly]
public sealed class DiscoveryAnalysisBoundUserInterface : BoundUserInterface
{
    private DiscoveryAnalysisWindow? _window;

    public DiscoveryAnalysisBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DiscoveryAnalysisWindow>();
        _window.DecodePressed += () => SendMessage(new DiscoveryStartDecodeMessage());
        _window.UploadPressed += () => SendMessage(new DiscoveryUploadMessage());
        _window.ClearPressed += () => SendMessage(new DiscoveryClearDiskMessage());
        _window.ConvertPressed += () => SendMessage(new DiscoveryConvertDuplicateMessage());
        _window.EjectPressed += () => SendMessage(new DiscoveryEjectDiskMessage());
        _window.ServerPressed += () => SendMessage(new Content.Shared.Research.Components.ConsoleServerSelectionMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DiscoveryAnalysisBoundUserInterfaceState s)
            _window?.UpdateState(s);
    }
}

public sealed class DiscoveryAnalysisWindow : LunaWindow
{
    public event Action? DecodePressed;
    public event Action? UploadPressed;
    public event Action? ClearPressed;
    public event Action? ConvertPressed;
    public event Action? EjectPressed;
    public event Action? ServerPressed;

    private readonly RichTextLabel _serverLabel;
    private readonly PanelContainer _serverStatusDot;
    private readonly RichTextLabel _diskLabel;
    private readonly RichTextLabel _detailLabel;
    private readonly ProgressBar _progress;
    private readonly Label _progressLabel;
    private readonly PanelContainer _stageBanner;
    private readonly Label _stageBannerLabel;
    private readonly Button _decode;
    private readonly Button _upload;
    private readonly Button _clear;
    private readonly Button _convert;
    private readonly Button _eject;
    private bool _progressStyledDecoding;
    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();

    public DiscoveryAnalysisWindow()
    {
        Title = Loc.GetString("discovery-research-analysis-title");
        MinSize = SetSize = new Vector2(580, 440);
        ApplyLunaChrome();

        var shell = new PanelContainer
        {
            PanelOverride = LunaWindowStyle.Shell(),
            Margin = new Thickness(4, 2, 4, 4),
        };
        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(10, 8, 10, 10),
        };

        var titleRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var title = new Label { Text = Loc.GetString("discovery-research-analysis-title"), HorizontalExpand = true };
        LunaWindowStyle.StyleHeading(title);
        var (server, serverDot) = ResearchServerStatusDot.CreateSelectButton(
            Loc.GetString("discovery-research-server-button"),
            minHeight: 28,
            minWidth: 160);
        server.OnPressed += _ => ServerPressed?.Invoke();
        _serverStatusDot = serverDot;
        titleRow.AddChild(title);
        titleRow.AddChild(server);
        root.AddChild(titleRow);

        var div = new PanelContainer { MinHeight = 1, MaxHeight = 1 };
        LunaWindowStyle.StyleDivider(div);
        root.AddChild(div);

        _stageBanner = new PanelContainer
        {
            MinHeight = 40,
            PanelOverride = LunaWindowStyle.Panel(),
        };
        _stageBannerLabel = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleHeading(_stageBannerLabel);
        _stageBanner.AddChild(_stageBannerLabel);
        root.AddChild(_stageBanner);

        var infoPanel = new PanelContainer { PanelOverride = LunaWindowStyle.Panel() };
        var infoCol = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10),
        };
        _serverLabel = new RichTextLabel();
        _diskLabel = new RichTextLabel();
        _detailLabel = new RichTextLabel();
        infoCol.AddChild(_serverLabel);
        infoCol.AddChild(_diskLabel);
        infoCol.AddChild(_detailLabel);
        infoPanel.AddChild(infoCol);
        root.AddChild(infoPanel);

        _progress = new ProgressBar();
        LunaWindowStyle.StyleProgressCooldown(_progress, 16f);
        _progressLabel = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleSecondary(_progressLabel);
        root.AddChild(_progress);
        root.AddChild(_progressLabel);

        var row1 = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _decode = new Button { Text = Loc.GetString("discovery-research-decode-button"), HorizontalExpand = true, MinHeight = 34 };
        _decode.OnPressed += _ => DecodePressed?.Invoke();
        _upload = new Button { Text = Loc.GetString("discovery-research-upload-button"), HorizontalExpand = true, MinHeight = 34 };
        _upload.OnPressed += _ => UploadPressed?.Invoke();
        row1.AddChild(_decode);
        row1.AddChild(_upload);
        root.AddChild(row1);

        var row2 = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _clear = new Button { Text = Loc.GetString("discovery-research-clear-button"), HorizontalExpand = true, MinHeight = 34 };
        _clear.OnPressed += _ => ClearPressed?.Invoke();
        _convert = new Button { Text = Loc.GetString("discovery-research-convert-button"), HorizontalExpand = true, MinHeight = 34 };
        _convert.OnPressed += _ => ConvertPressed?.Invoke();
        _eject = new Button { Text = Loc.GetString("discovery-research-eject-button"), HorizontalExpand = true, MinHeight = 34 };
        _eject.OnPressed += _ => EjectPressed?.Invoke();
        row2.AddChild(_clear);
        row2.AddChild(_convert);
        row2.AddChild(_eject);
        root.AddChild(row2);

        foreach (var b in new[] { _decode, _upload, _clear, _convert, _eject })
            LunaWindowStyle.ApplyCompactStyle(b);

        var footerDiv = new PanelContainer { MinHeight = 1, MaxHeight = 1 };
        LunaWindowStyle.StyleDivider(footerDiv);
        root.AddChild(footerDiv);

        shell.AddChild(root);
        ContentsContainer.AddChild(shell);
    }

    public void UpdateState(DiscoveryAnalysisBoundUserInterfaceState state)
    {
        var (stageText, stageColor) = state.DiskStage switch
        {
            ResearchDataDiskStage.Empty => (Loc.GetString("discovery-research-stage-empty"), LunaWindowStyle.TextMuted),
            ResearchDataDiskStage.Raw => (Loc.GetString("discovery-research-stage-raw"), LunaWindowStyle.AccentWarn),
            ResearchDataDiskStage.Decoded => (Loc.GetString("discovery-research-stage-decoded"), LunaWindowStyle.AccentGood),
            _ => (state.DiskStage.ToString(), LunaWindowStyle.TextMuted),
        };

        _stageBannerLabel.Text = stageText;
        _stageBannerLabel.FontColorOverride = stageColor;
        _stageBanner.PanelOverride = LunaWindowStyle.Box(
            Color.InterpolateBetween(stageColor, LunaWindowStyle.PanelBg, 0.7f),
            stageColor);

        _serverLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
            state.HasServer
                ? $"[color={LunaWindowStyle.TextSecondary.ToHex()}]{Loc.GetString("discovery-research-analysis-server", ("points", state.ServerPoints), ("working", state.WorkingComputeCount), ("linked", state.LinkedComputeCount))}[/color]"
                : $"[color={LunaWindowStyle.AccentBad.ToHex()}]{Loc.GetString("discovery-research-need-server")}[/color]"));
        ResearchServerStatusDot.Set(_serverStatusDot, state.HasServer);

        _diskLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
            state.HasDisk
                ? $"[color={stageColor.ToHex()}]{Loc.GetString("discovery-research-disk-stage", ("stage", Loc.GetString($"discovery-research-disk-stage-{state.DiskStage.ToString().ToLowerInvariant()}")))}[/color]"
                : $"[color={LunaWindowStyle.AccentBad.ToHex()}]{Loc.GetString("discovery-research-no-disk")}[/color]"));

        if (state.DiskStage == ResearchDataDiskStage.Raw)
        {
            _detailLabel.Visible = true;
            _detailLabel.MaxWidth = 540;
            _detailLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
                $"[color={LunaWindowStyle.AccentWarn.ToHex()}]{Loc.GetString("discovery-research-raw-preview", ("discipline", DiscoveryUiLoc.DisciplineName(state.PreviewDiscipline, _prototypes)), ("cost", state.PreviewCostEstimate), ("risk", DiscoveryUiLoc.RiskName(state.PreviewRisk)))}[/color]"));
        }
        else if (state.DiskStage == ResearchDataDiskStage.Decoded)
        {
            _detailLabel.Visible = true;
            _detailLabel.MaxWidth = 540;
            var dup = state.AlreadyUnlocked
                ? Loc.GetString("discovery-research-already-unlocked")
                : Loc.GetString("discovery-research-ready-upload");
            var dupColor = state.AlreadyUnlocked ? LunaWindowStyle.AccentWarn : LunaWindowStyle.AccentGood;
            var discName = DiscoveryUiLoc.DisciplineName(state.Discipline, _prototypes);
            _detailLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(
                $"[color={LunaWindowStyle.AccentGood.ToHex()}][bold]{state.TechName}[/bold][/color]\n" +
                $"[color={LunaWindowStyle.TextSecondary.ToHex()}]{discName} | {state.Cost} | {state.RecipeIds.Count}[/color]\n" +
                $"[color={dupColor.ToHex()}]{dup}[/color]"));
        }
        else
            _detailLabel.Visible = false;

        if (state.Decoding != _progressStyledDecoding)
        {
            _progressStyledDecoding = state.Decoding;
            if (state.Decoding)
                LunaWindowStyle.StyleProgressConfirm(_progress, 16f);
            else
                LunaWindowStyle.StyleProgressCooldown(_progress, 16f);
        }

        var decodeFraction = state.Decoding && state.DecodeDuration > 0f
            ? Math.Clamp(state.DecodeProgress / state.DecodeDuration, 0f, 1f)
            : 0f;
        _progress.MinValue = 0f;
        _progress.MaxValue = 1f;
        _progress.Value = decodeFraction;

        _progressLabel.Text = state.Decoding
            ? Loc.GetString("discovery-research-decode-progress", ("percent", (decodeFraction * 100f).ToString("0")))
            : Loc.GetString("discovery-research-analysis-idle");

        _decode.Disabled = state.Decoding || state.DiskStage != ResearchDataDiskStage.Raw || !state.HasServer;
        _upload.Disabled = state.Decoding || state.DiskStage != ResearchDataDiskStage.Decoded || state.AlreadyUnlocked || !state.HasServer;
        _convert.Disabled = state.Decoding || state.DiskStage != ResearchDataDiskStage.Decoded || !state.HasServer;
        _clear.Disabled = state.Decoding || !state.HasDisk || state.DiskStage == ResearchDataDiskStage.Empty;
        _eject.Disabled = state.Decoding || !state.HasDisk;
    }
}
