using Content.Shared._Lua.Reputation;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Content.Client.Examine;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Lua.Reputation;

public sealed class ReputationSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    private ReputationReasonWindow? _reasonWindow;

    public override void Initialize()
    { base.Initialize(); }

    public void AddReputationButtons(EntityUid player, EntityUid target, BoxContainer clickExamineBox)
    {
        if (player == target) return;
        var targetName = Identity.Name(target, EntityManager, player);
        clickExamineBox.AddChild(MakeReputationButton(target, targetName, ReputationVoteValue.Like));
        clickExamineBox.AddChild(MakeReputationButton(target, targetName, ReputationVoteValue.Dislike));
    }

    private ExamineButton MakeReputationButton(EntityUid target, string targetName, ReputationVoteValue value)
    {
        var like = value == ReputationVoteValue.Like;
        var verb = new ExamineVerb
        {
            Text = Loc.GetString(like ? "reputation-verb-like" : "reputation-verb-dislike"),
            Message = Loc.GetString(like ? "reputation-verb-like-message" : "reputation-verb-dislike-message", ("target", targetName)),
            Icon = new SpriteSpecifier.Texture(new(like ? "/Textures/_Lua/Interface/like.png" : "/Textures/_Lua/Interface/dislike.png")),
            ClientExclusive = true,
        };
        var button = new ExamineButton(verb, _sprite);
        button.OnPressed += _ => OpenReasonWindow(target, targetName, value);
        return button;
    }

    private void Submit(EntityUid target, ReputationVoteValue value, string? comment)
    {
        RaiseNetworkEvent(new SubmitPlayerReputationVoteEvent(GetNetEntity(target), value, comment));
    }

    private void OpenReasonWindow(EntityUid target, string targetName, ReputationVoteValue value)
    {
        _reasonWindow?.Close();
        _reasonWindow = new ReputationReasonWindow(targetName, value);
        _reasonWindow.Submitted += reason => Submit(target, value, reason);
        _reasonWindow.OnClose += () => _reasonWindow = null;
        _ui.WindowRoot.AddChild(_reasonWindow);
        _reasonWindow.OpenCentered();
    }
}
