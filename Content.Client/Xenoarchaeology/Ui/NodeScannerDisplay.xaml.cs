using System.Numerics;
using Content.Lua.UIKit.Styles;
using Content.Shared.NameIdentifier;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Xenoarchaeology.Ui;

/// Portable node scanner — Luna-styled discovery readout with effects.
public sealed class NodeScannerDisplay : LunaWindow
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedXenoArtifactSystem _artifact;
    private readonly Label _linkLabel;
    private readonly Label _stateLabel;
    private readonly BoxContainer _nodeList;
    private readonly Label _emptyLabel;

    private TimeSpan? _nextUpdate;
    private EntityUid _owner;
    private TimeSpan _updateFromAttachedFrequency = TimeSpan.FromSeconds(0.5);

    public NodeScannerDisplay()
    {
        IoCManager.InjectDependencies(this);
        _artifact = _ent.System<SharedXenoArtifactSystem>();

        Title = Loc.GetString("node-scan-display-title");
        MinSize = SetSize = new Vector2(360, 320);
        ApplyLunaChrome();

        var shell = new PanelContainer
        {
            PanelOverride = LunaWindowStyle.Shell(),
            Margin = new Thickness(4, 2, 4, 4),
        };
        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10, 8, 10, 10),
        };

        _linkLabel = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleSecondary(_linkLabel);
        _stateLabel = new Label { HorizontalAlignment = HAlignment.Center };
        LunaWindowStyle.StyleTiny(_stateLabel);

        _emptyLabel = new Label
        {
            Text = Loc.GetString("node-scan-no-data"),
            HorizontalAlignment = HAlignment.Center,
        };
        LunaWindowStyle.StyleMuted(_emptyLabel);

        _nodeList = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            VerticalExpand = true,
        };

        root.AddChild(_linkLabel);
        root.AddChild(new PanelContainer
        {
            MinHeight = 1,
            MaxHeight = 1,
            PanelOverride = new StyleBoxFlat { BackgroundColor = LunaWindowStyle.Divider },
        });
        root.AddChild(_stateLabel);
        root.AddChild(_emptyLabel);
        root.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _nodeList },
        });

        shell.AddChild(root);
        ContentsContainer.AddChild(shell);
    }

    public void SetOwner(EntityUid scannerEntityUid)
    {
        if (!_ent.TryGetComponent<NodeScannerComponent>(scannerEntityUid, out var scannerComponent))
        {
            Close();
            return;
        }

        _updateFromAttachedFrequency = scannerComponent.DisplayDataUpdateInterval;
        _owner = scannerEntityUid;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_nextUpdate != null && _timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + _updateFromAttachedFrequency;

        if (!_ent.TryGetComponent(_owner, out NodeScannerConnectedComponent? connectedScanner))
        {
            Update(false, ArtifactState.None, null);
            return;
        }

        var attachedArtifactEnt = connectedScanner.AttachedTo;
        if (!_ent.TryGetComponent(attachedArtifactEnt, out XenoArtifactComponent? artifactComponent))
        {
            Update(false, ArtifactState.None, null);
            return;
        }

        _ent.TryGetComponent(attachedArtifactEnt, out XenoArtifactUnlockingComponent? unlockingComponent);

        ArtifactState artifactState;
        if (unlockingComponent == null)
        {
            var timeToUnlockAvailable = artifactComponent.NextUnlockTime - _timing.CurTime;
            artifactState = timeToUnlockAvailable > TimeSpan.Zero
                ? ArtifactState.Cooldown
                : ArtifactState.Ready;
        }
        else
            artifactState = ArtifactState.Unlocking;

        var art = new Entity<XenoArtifactComponent>(attachedArtifactEnt, artifactComponent);
        var rows = new List<(string Id, bool Locked, bool Active, string Effect, string Tip)>();
        foreach (var node in _artifact.GetAllNodes(art))
        {
            var id = (_ent.GetComponentOrNull<NameIdentifierComponent>(node)?.Identifier ?? 0).ToString("D3");
            var effect = _ent.GetComponentOrNull<MetaDataComponent>(node)?.EntityDescription ?? string.Empty;
            var tip = node.Comp.TriggerTip is { } tipId ? Loc.GetString(tipId) : string.Empty;
            rows.Add((id, node.Comp.Locked, _artifact.IsNodeActive(art, node), effect, tip));
        }

        Update(true, artifactState, rows);
    }

    private void Update(
        bool isConnected,
        ArtifactState artifactState,
        List<(string Id, bool Locked, bool Active, string Effect, string Tip)>? nodes)
    {
        _stateLabel.Text = GetStateText(artifactState);
        _linkLabel.Text = isConnected
            ? Loc.GetString("node-scanner-artifact-connected")
            : Loc.GetString("node-scanner-artifact-non-connected");
        _linkLabel.FontColorOverride = isConnected ? LunaWindowStyle.AccentGood : LunaWindowStyle.AccentBad;

        _nodeList.RemoveAllChildren();

        if (nodes == null || nodes.Count == 0)
        {
            _emptyLabel.Visible = true;
            return;
        }

        _emptyLabel.Visible = false;
        foreach (var (id, locked, active, effect, tip) in nodes)
        {
            var color = active
                ? LunaWindowStyle.AccentGood
                : locked
                    ? LunaWindowStyle.TextMuted
                    : LunaWindowStyle.Accent;

            var row = new PanelContainer
            {
                PanelOverride = LunaWindowStyle.Box(LunaWindowStyle.PanelBg, LunaWindowStyle.PanelBorder),
                Margin = new Thickness(0, 1),
            };
            var col = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 2,
                Margin = new Thickness(8, 4),
            };

            var head = new Label
            {
                Text = active ? $"#{id} ACTIVE" : locked ? $"#{id}*" : $"#{id}",
                FontColorOverride = color,
            };
            LunaWindowStyle.StyleTiny(head);
            col.AddChild(head);

            if (!string.IsNullOrWhiteSpace(effect))
            {
                var effectLabel = new Label
                {
                    Text = effect,
                    MaxWidth = 300,
                    FontColorOverride = LunaWindowStyle.TextPrimary,
                };
                LunaWindowStyle.StyleTiny(effectLabel);
                col.AddChild(effectLabel);
            }

            if (!string.IsNullOrWhiteSpace(tip))
            {
                var tipLabel = new Label
                {
                    Text = tip,
                    MaxWidth = 300,
                    FontColorOverride = LunaWindowStyle.TextMuted,
                };
                LunaWindowStyle.StyleTiny(tipLabel);
                col.AddChild(tipLabel);
            }

            row.AddChild(col);
            _nodeList.AddChild(row);
        }
    }

    private static string GetStateText(ArtifactState state)
    {
        return state switch
        {
            ArtifactState.None => "\u2800",
            ArtifactState.Ready => Loc.GetString("node-scanner-artifact-state-ready"),
            ArtifactState.Unlocking => Loc.GetString("node-scanner-artifact-state-unlocking"),
            ArtifactState.Cooldown => Loc.GetString("node-scanner-artifact-state-cooldown"),
            _ => string.Empty,
        };
    }
}
