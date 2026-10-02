using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.ErpPanel;
using Content.Shared._Lua.Chat.Systems;
using Content.Shared._Lua.ERP;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Arcane.ErpPanel;

public sealed class ErpPanelSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _ticking = default!;
    [Dependency] private readonly ArousalSystem _arousal = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly EntProtoId HeartsProto = new("EffectHearts");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErpPanelOwnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ErpPanelOwnerComponent, ErpPanelOpenEvent>(OnOpenPanel);
        SubscribeLocalEvent<ErpPanelOwnerComponent, BoundUIClosedEvent>(OnBoundUIClosed);
        SubscribeLocalEvent<ErpPanelOwnerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

        Subs.BuiEvents<ErpPanelOwnerComponent>(ErpPanelKey.Key, subs =>
        {
            subs.Event<ErpPanelSendMessage>(OnSendMessage);
        });
    }

    private void OnMapInit(Entity<ErpPanelOwnerComponent> entity, ref MapInitEvent args)
    {
        var interfaceData = new InterfaceData(
            clientType: "Content.Client._Arcane.ErpPanel.ErpPanelWindowBUI"
        );

        _ui.SetUi(entity.Owner, ErpPanelKey.Key, interfaceData);
    }

    private void OnOpenPanel(Entity<ErpPanelOwnerComponent> entity, ref ErpPanelOpenEvent args)
    {
        if (!IsValidUi(entity.Owner, args.Target))
            return;

        if (!_interaction.IsAccessible(entity.Owner, args.Target))
            return;

        TryOpenPanel(entity.Owner, args.Target);
        entity.Comp.Target = args.Target;
    }

    private void OnBoundUIClosed(Entity<ErpPanelOwnerComponent> entity, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not ErpPanelKey.Key)
            return;

        entity.Comp.Target = null;
    }

    private void OnSendMessage(Entity<ErpPanelOwnerComponent> entity, ref ErpPanelSendMessage args)
    {
        var user = args.Actor;
        var target = entity.Comp.Target;
        if (target == null || user != entity.Owner)
            return;

        ProccessInteraction(user, target.Value, args.Interaction, args.CustomArousal, args.CustomMoaning);
    }

    private void OnGetVerbs(
        Entity<ErpPanelOwnerComponent> ent,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!HasComp<ErpPanelOwnerComponent>(args.User))
            return;

        if (!HasComp<ArousalComponent>(args.User))
            return;

        if (!HasComp<ArousalComponent>(ent.Owner))
            return;

        if (!CanUseErp(args.User, ent.Owner))
            return;

        var user = args.User;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                var ev = new ErpPanelOpenEvent(ent.Owner);
                RaiseLocalEvent(user, ref ev);
            },
            Text = Loc.GetString("erp-panel-open-verb"),
            Icon = new SpriteSpecifier.Texture(
                new("/Textures/_Arcane/Interface/heartIcon.png")),
            Priority = 2
        };

        args.Verbs.Add(verb);
    }

    public void ProccessInteraction(EntityUid user, EntityUid target, string interactionId, float customArousal, float customMoaning)
    {
        if (!_prototype.TryIndex<PanelInteractionPrototype>(interactionId, out var interaction))
            return;

        if (!IsValidInteraction(user, target, interaction))
            return;

        if (!TryComp<ErpPanelOwnerComponent>(user, out var userPanel))
            return;

        if (!CheckRequirements(user, target, interaction))
            return;

        customArousal = Math.Clamp(customArousal, 0, 300);
        customMoaning = Math.Clamp(customMoaning, 0, 300);

        if (interaction.TargetArouse != 0 && TryComp<ArousalComponent>(target, out var targetArousalComp))
        {
            var targetDelta = interaction.TargetArouse * customArousal / 100;
            var currentTargetArousal = _arousal.GetArousal(targetArousalComp);

            if (targetDelta < 0 && Math.Abs(targetDelta) > currentTargetArousal)
                targetDelta = -currentTargetArousal;

            if (targetDelta > 0)
                Spawn(HeartsProto, _transform.GetMapCoordinates(target));

            _arousal.AddArousal(target, targetDelta);
            ProccessMoan(target, customMoaning);
        }

        userPanel.Cooldowns[interaction.ID] = _ticking.CurTime;

        ProccessMessages(user, target, interaction);
        ProccessSounds(user, interaction);
        ProccessLunge(user, target);

        if (user == target)
            return;

        if (interaction.UserArouse != 0 && TryComp<ArousalComponent>(user, out var userArousalComp))
        {
            var userDelta = interaction.UserArouse * customArousal / 100;
            var currentUserArousal = _arousal.GetArousal(userArousalComp);

            if (userDelta < 0 && Math.Abs(userDelta) > currentUserArousal)
                userDelta = -currentUserArousal;

            _arousal.AddArousal(user, userDelta);
            ProccessMoan(user, customMoaning);
        }
    }

    public void ProccessMoan(EntityUid uid, float customMoaning)
    {
        if (!TryComp<ArousalComponent>(uid, out var userArousal) || userArousal.LastValue <= 0 || userArousal.MaxArousal == 0)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var userHumanoid))
            return;

        var userMoanChance = userArousal.LastValue / userArousal.MaxArousal * customMoaning / 100f;
        userMoanChance = Math.Clamp(userMoanChance, 0f, 1f);

        if (_random.Prob(userMoanChance))
            MoanWithGender(uid, userHumanoid.Gender, userArousal.LastValue / userArousal.MaxArousal);
    }

    private void MoanWithGender(EntityUid uid, Gender userHumanoid, float arousalPercent)
    {
        var collection = ErpAudio.MoanSounds.GetValueOrDefault(userHumanoid, ErpAudio.MoanSounds[Gender.Female]);

        if (!_prototype.TryIndex(collection, out var soundCollection))
            return;

        if (soundCollection.PickFiles.Count == 0)
            return;

        arousalPercent = Math.Clamp(arousalPercent, 0f, 1f);

        var index = (int) Math.Ceiling(arousalPercent * soundCollection.PickFiles.Count) - 1;
        index += _random.Next(-2, 1);
        index = Math.Clamp(index, 0, soundCollection.PickFiles.Count - 1);

        var audioParams = new AudioParams()
        {
            Variation = 0.125f,
            MaxDistance = 4f,
        };

        _audio.PlayPvs(new ResolvedCollectionSpecifier(collection, index), uid, audioParams);

        _chat.TrySendInGameICMessage(uid, Loc.GetString("moan-message"), InGameICChatType.Emote, true);
    }

    private void ProccessMessages(EntityUid user, EntityUid target, PanelInteractionPrototype interaction)
    {
        var messagesCollection = user == target ? interaction.SelfMessages : interaction.Messages;

        var message = _random.Pick(messagesCollection)
            .Replace("$target", Identity.Name(target, EntityManager, user));

        _chat.TrySendInGameICMessage(user, message, InGameICChatType.Emote, false);
    }

    private void ProccessLunge(EntityUid user, EntityUid target)
    {

        if (user == target)
            return;

        var userXform = Transform(user);

        var invMatrix = _transform.GetInvWorldMatrix(userXform);
        var localPos = Vector2.Transform(_transform.GetMapCoordinates(target).Position, invMatrix);

        if (localPos.LengthSquared() <= 0f)
            return;

        localPos = userXform.LocalRotation.RotateVec(localPos);

        RaiseNetworkEvent(
            new ErpLungeEvent(GetNetEntity(user), localPos),
            Filter.Pvs(user, entityManager: EntityManager));
    }

    private void ProccessSounds(EntityUid user, PanelInteractionPrototype interaction)
    {
        if (interaction.Sounds.Count == 0)
            return;

        var resSound = _random.Pick(interaction.Sounds);
        var sound = new SoundPathSpecifier(resSound);
        _audio.PlayPvs(sound, user);
    }

    private void TryOpenPanel(EntityUid user, EntityUid target)
    {
        if (!IsValidUi(user, target))
            return;

        _ui.TryOpenUi(user, ErpPanelKey.Key, user);

        var state = new ErpPanelBuiState(GetNetEntity(user), GetNetEntity(target));
        _ui.SetUiState(user, ErpPanelKey.Key, state);
    }

    private bool IsValidUi(EntityUid user, EntityUid target)
    {
        if (!HasComp<ErpPanelOwnerComponent>(user))
            return false;

        if (!HasComp<ErpPanelOwnerComponent>(target))
            return false;

        if (!HasComp<ArousalComponent>(user) || !HasComp<ArousalComponent>(target))
            return false;

        if (!CanUseErp(user, target))
            return false;

        return true;
    }

    private bool IsValidInteraction(EntityUid user, EntityUid target, PanelInteractionPrototype interaction)
    {
        if (!_interaction.InRangeAndAccessible(user, target, interaction.Range))
            return false;

        if (user == target && interaction.SelfMessages.Count == 0)
            return false;

        if (user != target && interaction.Messages.Count == 0)
            return false;

        if (!TryComp<ErpPanelOwnerComponent>(user, out var userPanel))
            return false;

        if (!HasComp<ErpPanelOwnerComponent>(target))
            return false;

        if (!HasComp<ArousalComponent>(user) || !HasComp<ArousalComponent>(target))
            return false;

        if (IsErpInteraction(interaction) && !CanUseErp(user, target))
            return false;

        if (userPanel.Cooldowns.TryGetValue(interaction.ID, out var lastUse) && lastUse + interaction.Cooldown > _ticking.CurTime)
            return false;

        return true;
    }

    private static bool IsErpInteraction(PanelInteractionPrototype interaction)
    {
        return interaction.Tags.Contains(ErpPanelConstants.ErpInteractionTag);
    }

    private bool CanUseErp(EntityUid user, EntityUid target)
    {
        if (!TryComp<ErpStatusComponent>(user, out var userStatus) || userStatus.Status == EnumERPStatus.NO)
            return false;

        if (user == target)
            return true;

        return TryComp<ErpStatusComponent>(target, out var targetStatus)
               && targetStatus.Status != EnumERPStatus.NO;
    }

    private bool CheckRequirements(EntityUid user, EntityUid target, PanelInteractionPrototype interaction)
    {
        var passed = true;

        if (interaction.UserRequirements != null)
        {
            foreach (var requirement in interaction.UserRequirements)
            {
                if (!requirement.IsAvailable(user, EntityManager))
                    passed = false;
            }
        }

        if (interaction.TargetRequirements != null)
        {
            foreach (var requirement in interaction.TargetRequirements)
            {
                if (!requirement.IsAvailable(target, EntityManager))
                    passed = false;
            }
        }

        return passed;
    }
}
