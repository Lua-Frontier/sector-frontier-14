using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Lua.Reputation;
using Content.Shared.Database;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Lua.Reputation;

public sealed class ReputationModerationWindow : DefaultWindow
{
    private readonly Label _targetKindLabel;
    private readonly Label _scoreLabel;
    private readonly Label _votesCountLabel;
    private readonly BoxContainer _votesContainer;
    public event Action<int, string>? DeleteRequested;
    public ReputationModerationWindow()
    {
        MinSize = SetSize = new Vector2(720, 560);
        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
        };
        Contents.AddChild(root);
        var summaryPanel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#1D242B"),
                BorderColor = Color.FromHex("#46525F"),
                BorderThickness = new Thickness(1),
            },
            HorizontalExpand = true,
        };
        root.AddChild(summaryPanel);
        var summary = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            Margin = new Thickness(10, 8),
            HorizontalExpand = true,
        };
        summaryPanel.AddChild(summary);
        _targetKindLabel = new Label
        { HorizontalExpand = true, };
        summary.AddChild(_targetKindLabel);
        _scoreLabel = new Label
        {
            MinWidth = 96,
            Align = Label.AlignMode.Center,
        };
        summary.AddChild(_scoreLabel);
        _votesCountLabel = new Label
        {
            MinWidth = 140,
            Align = Label.AlignMode.Right,
        };
        summary.AddChild(_votesCountLabel);
        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(scroll);
        _votesContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(_votesContainer);
    }

    public void UpdateState(ReputationModerationEuiState state)
    {
        Title = Loc.GetString("reputation-admin-window-title", ("target", state.Summary.Name));
        _targetKindLabel.Text = Loc.GetString(GetTargetKindLocId(state.Summary.Kind), ("target", state.Summary.Name));
        _scoreLabel.Text = FormatScore(state.Summary.Score);
        _scoreLabel.FontColorOverride = GetScoreColor(state.Summary.Score);
        _votesCountLabel.Text = Loc.GetString("reputation-admin-active-votes", ("votes", state.Summary.ActiveVotes));
        _votesContainer.RemoveAllChildren();
        if (state.Votes.Count == 0)
        {
            _votesContainer.AddChild(new Label { Text = Loc.GetString("reputation-admin-empty") });
            return;
        }
        foreach (var vote in state.Votes)
        { _votesContainer.AddChild(MakeVoteControl(vote)); }
    }

    private Control MakeVoteControl(ReputationVoteDetails vote)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = vote.Deleted ? Color.FromHex("#241F20") : Color.FromHex("#171B1F"),
                BorderColor = vote.Value == ReputationVoteValue.Like ? Color.FromHex("#3C6D4E") : Color.FromHex("#7A4A4A"),
                BorderThickness = new Thickness(1),
            },
            HorizontalExpand = true,
        };
        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };
        panel.AddChild(root);
        var header = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        root.AddChild(header);
        header.AddChild(new Label
        {
            Text = GetVoteText(vote.Value),
            FontColorOverride = GetScoreColor((int) vote.Value),
            MinWidth = 36,
            Align = Label.AlignMode.Center,
        });
        header.AddChild(MakeWrappedLabel(Loc.GetString("reputation-admin-vote-line", ("voter", vote.VoterName), ("time", vote.CreatedAt.ToLocalTime().ToString("g"))), 560));
        if (!string.IsNullOrWhiteSpace(vote.Comment))
        { root.AddChild(MakeWrappedLabel(Loc.GetString("reputation-admin-vote-comment", ("comment", vote.Comment)), 650)); }
        if (vote.Deleted)
        {
            root.AddChild(MakeWrappedLabel(Loc.GetString("reputation-admin-vote-deleted", ("reason", vote.DeleteReason ?? string.Empty)), 650));
            return panel;
        }
        var controls = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        root.AddChild(controls);
        var reasonEdit = new TextEdit
        {
            MinHeight = 42,
            HorizontalExpand = true,
            Placeholder = new Rope.Leaf(Loc.GetString("reputation-admin-delete-reason-placeholder")),
        };
        controls.AddChild(reasonEdit);
        var deleteButton = new Button
        {
            Text = Loc.GetString("reputation-admin-delete"),
            VerticalAlignment = VAlignment.Center,
        };
        deleteButton.OnPressed += _ =>
        {
            var reason = Rope.Collapse(reasonEdit.TextRope).Trim();
            if (reason.Length == 0) return;
            DeleteRequested?.Invoke(vote.Id, reason);
        };
        controls.AddChild(deleteButton);
        return panel;
    }

    private static RichTextLabel MakeWrappedLabel(string text, float maxWidth)
    {
        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            MaxWidth = maxWidth,
        };
        label.SetMessage(FormattedMessage.FromUnformatted(text));
        return label;
    }

    private static string GetTargetKindLocId(ReputationTargetKind kind)
    {
        return kind switch
        { ReputationTargetKind.Admin => "reputation-admin-target-admin", _ => "reputation-admin-target-player", };
    }

    private static string FormatScore(int score)
    { return score > 0 ? $"+{score}" : score.ToString(); }

    private static Color GetScoreColor(int score)
    {
        if (score > 0) return Color.FromHex("#77D38B");
        return score < 0 ? Color.FromHex("#E57B75") : Color.FromHex("#D7DCE2");
    }

    private static string GetVoteText(ReputationVoteValue value)
    {
        return value switch
        {
            ReputationVoteValue.Like => "+1",
            ReputationVoteValue.Dislike => "-1",
            _ => "0",
        };
    }
}
