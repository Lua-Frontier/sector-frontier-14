using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Database;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Lua.Reputation;

public sealed class ReputationReasonWindow : DefaultWindow
{
    private readonly ReputationVoteValue _value;
    private readonly TextEdit _reasonEdit;
    private readonly Label _counterLabel;
    private readonly Button _submitButton;

    public event Action<string?>? Submitted;

    public ReputationReasonWindow(string targetName, ReputationVoteValue value)
    {
        _value = value;
        Title = Loc.GetString(value == ReputationVoteValue.Like ? "reputation-window-title-like" : "reputation-window-title-dislike", ("target", targetName));
        MinSize = SetSize = new Vector2(520, 360);
        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
        };
        Contents.AddChild(root);
        var hint = new RichTextLabel
        {
            HorizontalExpand = true,
            MaxWidth = 480,
        };
        hint.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString(value == ReputationVoteValue.Like ? "reputation-window-reason-label-like" : "reputation-window-reason-label-dislike", ("min", ReputationConstants.MinNegativeCommentLength))));
        root.AddChild(hint);
        _reasonEdit = new TextEdit
        {
            MinHeight = 190,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _reasonEdit.OnTextChanged += _ => UpdateSubmitState();
        root.AddChild(_reasonEdit);
        _counterLabel = new Label();
        root.AddChild(_counterLabel);
        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalAlignment = HAlignment.Right,
        };
        root.AddChild(buttons);
        var cancelButton = new Button
        { Text = Loc.GetString("reputation-window-cancel"), };
        cancelButton.OnPressed += _ => Close();
        buttons.AddChild(cancelButton);
        _submitButton = new Button
        { Text = Loc.GetString("reputation-window-submit"), };
        _submitButton.OnPressed += _ => Submit();
        buttons.AddChild(_submitButton);
        UpdateSubmitState();
    }

    private void Submit()
    {
        var reason = Rope.Collapse(_reasonEdit.TextRope).Trim();
        if (_value == ReputationVoteValue.Dislike && reason.Length < ReputationConstants.MinNegativeCommentLength || reason.Length > ReputationConstants.MaxCommentLength) return;
        Submitted?.Invoke(string.IsNullOrWhiteSpace(reason) ? null : reason);
        Close();
    }

    private void UpdateSubmitState()
    {
        var length = Rope.Collapse(_reasonEdit.TextRope).Trim().Length;
        _counterLabel.Text = Loc.GetString("reputation-window-counter", ("current", length), ("min", _value == ReputationVoteValue.Dislike ? ReputationConstants.MinNegativeCommentLength : 0), ("max", ReputationConstants.MaxCommentLength));
        _submitButton.Disabled = _value == ReputationVoteValue.Dislike && length < ReputationConstants.MinNegativeCommentLength || length > ReputationConstants.MaxCommentLength;
    }
}
