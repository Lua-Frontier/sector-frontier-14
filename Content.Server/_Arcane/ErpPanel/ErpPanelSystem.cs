using System.Numerics;
using Content.Server.Administration.Logs; // Lua
using Content.Server.Chat.Managers; // Lua
using Content.Server.Chat.Systems;
using Content.Lua.Shared.CCVar; // Lua
using Content.Shared._Arcane.ERP;
using Content.Shared._Arcane.ErpPanel;
using Content.Shared.ActionBlocker; // Lua
using Content.Shared.Chat; // Lua
using Content.Shared.Chat.Systems;
using Content.Shared.Database; // Lua
using Content.Shared.Damage;
using Content.Shared.ERP;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems; // Lua
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration; // Lua
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
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IChatManager _chatManager = default!; // Lua
    [Dependency] private readonly IAdminLogManager _adminLogger = default!; // Lua
    [Dependency] private readonly INetConfigurationManager _netCfg = default!; // Lua
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!; // Lua
    [Dependency] private readonly MobStateSystem _mobState = default!; // Lua

    private static readonly EntProtoId HeartsProto = new("EffectHearts");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErpPanelOwnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ErpPanelOwnerComponent, ErpPanelOpenEvent>(OnOpenPanel);
        SubscribeLocalEvent<ErpPanelOwnerComponent, BoundUIClosedEvent>(OnBoundUIClosed);
        SubscribeLocalEvent<ErpPanelOwnerComponent, GetVerbsEvent<Verb>>(OnGetVerbs); // Lua

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
        if (!_interaction.IsAccessible(entity.Owner, args.Target))
            return;

        if (!TryOpenPanel(entity.Owner, args.Target)) // Lua
            return;

        entity.Comp.Target = args.Target;

        // Lua: arousal is only sent to its owner and the partner with the panel open
        if (TryComp<ArousalComponent>(args.Target, out var targetArousal))
            Dirty(args.Target, targetArousal);
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

        ProcessInteraction(user, target.Value, args.Interaction, args.PrivateMode); // Lua
    }

    private void OnGetVerbs(
        Entity<ErpPanelOwnerComponent> ent,
        ref GetVerbsEvent<Verb> args) // Lua
    {
        if (!IsValidUi(args.User, ent.Owner)) // Lua
            return;

        var user = args.User;

        Verb verb = new() // Lua
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

    public void ProcessInteraction(EntityUid user, EntityUid target, string interactionId, bool privateMode = false) // Lua
    {
        if (!_prototype.TryIndex<PanelInteractionPrototype>(interactionId, out var interaction))
            return;

        if (!TryComp<ErpPanelOwnerComponent>(user, out var userPanel))
            return;

        if (!IsValidInteraction(user, target, interaction, userPanel)) // Lua
            return;

        if (!CheckRequirements(user, target, interaction))
            return;

        // Lua start
        Filter? privateFilter = privateMode
            ? Filter.Entities(user, target)
            : null;
        // Lua end

        if (interaction.TargetArouse != 0 && TryComp<ArousalComponent>(target, out var targetArousalComp))
        {
            var targetDelta = interaction.TargetArouse * GetMultiplier(target, LuaCCVars.ErpArousalMultiplier) / 100; // Lua
            var currentTargetArousal = _arousal.GetArousal(targetArousalComp);

            if (targetDelta < 0 && Math.Abs(targetDelta) > currentTargetArousal)
                targetDelta = -currentTargetArousal;

            if (targetDelta > 0)
                Spawn(HeartsProto, _transform.GetMapCoordinates(target));

            _arousal.AddArousal(target, targetDelta, privateFilter: privateFilter); // Lua
            ProcessMoan(target, GetMultiplier(target, LuaCCVars.ErpMoanMultiplier), privateFilter); // Lua
        }

        userPanel.LastInteraction = _ticking.CurTime;

        // Lua start
        ProcessMessages(user, target, interaction, privateFilter);
        ProcessSounds(user, interaction, privateFilter);
        ProcessLunge(user, target, privateFilter);
        // Lua end

        if (user == target)
            return;

        if (interaction.Kind == PanelInteractionKind.Erotic)
        {
            ProcessContactDamage(user, target);
            ProcessContactDamage(target, user);
        }

        if (interaction.UserArouse != 0 && TryComp<ArousalComponent>(user, out var userArousalComp))
        {
            var userDelta = interaction.UserArouse * GetMultiplier(user, LuaCCVars.ErpArousalMultiplier) / 100; // Lua
            var currentUserArousal = _arousal.GetArousal(userArousalComp);

            if (userDelta < 0 && Math.Abs(userDelta) > currentUserArousal)
                userDelta = -currentUserArousal;

            _arousal.AddArousal(user, userDelta, privateFilter: privateFilter); // Lua
            ProcessMoan(user, GetMultiplier(user, LuaCCVars.ErpMoanMultiplier), privateFilter); // Lua
        }
    }

    private void ProcessContactDamage(EntityUid source, EntityUid victim)
    {
        if (!TryComp<ErpContactDamageComponent>(source, out var contact))
            return;

        if (TryComp<HumanoidAppearanceComponent>(source, out var sourceHumanoid) &&
            TryComp<HumanoidAppearanceComponent>(victim, out var victimHumanoid) &&
            sourceHumanoid.Species == victimHumanoid.Species)
            return;

        _damageable.TryChangeDamage(victim, contact.Damage, origin: source);

        if (_prototype.TryIndex(contact.Popups, out var popups))
            _popup.PopupEntity(Loc.GetString(_random.Pick(popups.Values)), victim, victim, PopupType.SmallCaution);
    }

    private void ProcessMoan(EntityUid uid, float moanMultiplier, Filter? privateFilter) // Lua
    {
        if (!TryComp<ArousalComponent>(uid, out var userArousal) || userArousal.LastValue <= 0 || userArousal.MaxArousal == 0)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(uid, out var userHumanoid))
            return;

        var userMoanChance = userArousal.LastValue / userArousal.MaxArousal * moanMultiplier / 100f;
        userMoanChance = Math.Clamp(userMoanChance, 0f, 1f);

        if (_random.Prob(userMoanChance))
            MoanWithGender(uid, userHumanoid.Gender, userArousal.LastValue / userArousal.MaxArousal, privateFilter); // Lua
    }

    private void MoanWithGender(EntityUid uid, Gender userHumanoid, float arousalPercent, Filter? privateFilter) // Lua
    {
        var collection = ErpAudio.GetMoanSounds(userHumanoid); // Lua

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

        // Lua start
        var sound = new ResolvedCollectionSpecifier(collection, index);
        if (privateFilter != null)
            _audio.PlayEntity(sound, privateFilter, uid, false, audioParams);
        else
            _audio.PlayPvs(sound, uid, audioParams);

        SendEmote(uid, Loc.GetString("moan-message"), privateFilter, true);
        // Lua end
    }

    private void ProcessMessages(EntityUid user, EntityUid target, PanelInteractionPrototype interaction, Filter? privateFilter) // Lua
    {
        var messagesId = user == target ? interaction.SelfMessages : interaction.Messages;
        if (!_prototype.TryIndex(messagesId, out var messages))
            return;

        var message = Loc.GetString(_random.Pick(messages.Values),
            ("target", Identity.Name(target, EntityManager, user)));

        SendEmote(user, message, privateFilter, false); // Lua
    }

    // Lua start
    public void SendEmote(EntityUid source, string message, Filter? privateFilter, bool hideChat)
    {
        if (privateFilter == null)
        {
            _chat.TrySendInGameICMessage(source, message, InGameICChatType.Emote, hideChat);
            return;
        }

        var ent = Identity.Entity(source, EntityManager);
        var wrapped = Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", FormattedMessage.EscapeText(Name(ent))),
            ("entity", ent),
            ("message", FormattedMessage.RemoveMarkupOrThrow(message)));

        _chatManager.ChatMessageToManyFiltered(privateFilter, ChatChannel.Emotes, message, wrapped, source, hideChat, false, null);

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Private ERP emote from {ToPrettyString(source):user}: {message}");
    }
    // Lua end

    private void ProcessLunge(EntityUid user, EntityUid target, Filter? privateFilter) // Lua
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
            privateFilter ?? Filter.Pvs(user, entityManager: EntityManager)); // Lua
    }

    private void ProcessSounds(EntityUid user, PanelInteractionPrototype interaction, Filter? privateFilter) // Lua
    {
        // Lua start
        if (privateFilter != null)
            _audio.PlayEntity(interaction.Sound, privateFilter, user, false);
        else
            _audio.PlayPvs(interaction.Sound, user);
        // Lua end
    }

    private bool TryOpenPanel(EntityUid user, EntityUid target) // Lua
    {
        if (!IsValidUi(user, target))
            return false;

        _ui.TryOpenUi(user, ErpPanelKey.Key, user);

        var state = new ErpPanelBuiState(GetNetEntity(user), GetNetEntity(target));
        _ui.SetUiState(user, ErpPanelKey.Key, state);
        return true;
    }

    private bool IsValidUi(EntityUid user, EntityUid target)
    {
        if (!HasComp<ErpPanelOwnerComponent>(user))
            return false;

        if (!HasComp<ErpPanelOwnerComponent>(target))
            return false;

        if (!HasComp<ArousalComponent>(user) || !HasComp<ArousalComponent>(target))
            return false;

        if (!IsPanelEnabled(user) || !IsPanelEnabled(target)) // Lua
            return false;

        if (_mobState.IsIncapacitated(user) || _mobState.IsIncapacitated(target)) // Lua
            return false;

        return true;
    }

    private bool IsValidInteraction(EntityUid user, EntityUid target, PanelInteractionPrototype interaction, ErpPanelOwnerComponent userPanel) // Lua
    {
        if (userPanel.LastInteraction + ErpPanelOwnerComponent.Cooldown > _ticking.CurTime)
            return false;

        if (user == target && interaction.SelfMessages == null)
            return false;

        if (user != target && interaction.Messages == null)
            return false;

        if (!_interaction.InRangeAndAccessible(user, target, interaction.Range))
            return false;

        if (!IsValidUi(user, target)) // Lua
            return false;

        if (interaction.IsErotic && !CanUseErp(user, target)) // Lua
            return false;

        // Lua: cuffed, asleep or stunned - Safe only
        if (interaction.Kind != PanelInteractionKind.Safe && !CanActFreely(user, target))
            return false;

        return true;
    }

    // Lua start
    private bool CanActFreely(EntityUid user, EntityUid target)
    {
        return _actionBlocker.CanInteract(user, target)
               && (user == target || _actionBlocker.CanInteract(target, null));
    }
    // Lua end

    // Lua start
    private bool IsPanelEnabled(EntityUid uid)
    {
        // SSD or abandoned body
        if (!TryComp<ActorComponent>(uid, out var actor))
            return false;

        return _netCfg.GetClientCVar(actor.PlayerSession.Channel, LuaCCVars.ErpPanelEnabled);
    }

    private float GetMultiplier(EntityUid uid, CVarDef<float> cvar)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return 100f;

        // NaN passes through Math.Clamp
        var value = _netCfg.GetClientCVar(actor.PlayerSession.Channel, cvar);
        return float.IsFinite(value) ? Math.Clamp(value, 0f, ErpPanelOwnerComponent.MaxMultiplier) : 100f;
    }
    // Lua end

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
        // Lua start
        if (interaction.UserRequirements != null)
        {
            foreach (var requirement in interaction.UserRequirements)
            {
                if (!requirement.IsAvailable(user, EntityManager))
                    return false;
            }
        }

        if (interaction.TargetRequirements != null)
        {
            foreach (var requirement in interaction.TargetRequirements)
            {
                if (!requirement.IsAvailable(target, EntityManager))
                    return false;
            }
        }

        return true;
        // Lua end
    }
}
