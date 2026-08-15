// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Instruments;
using Content.Server.Lathe.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Medical.Components;
using Content.Server.Research.Components;
using Content.Shared._Lua.Achievements;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Events;
using Content.Shared.Bible;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Body.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Carrying;
using Content.Shared.Chemistry.Events;
using Content.Shared.Cloning.Events;
using Content.Shared.Cuffs.Components;
using Content.Shared.Drunk;
using Content.Shared.Emag.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.Components.OnTrigger;
using Content.Shared.Fax;
using Content.Shared.Ghost;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Instruments;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Kitchen.Events;
using Content.Shared.Lathe;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Paper;
using Content.Shared.RCD;
using Content.Shared.Research.Components;
using Content.Shared.Roles;
using Content.Shared.StatusEffect;
using Content.Shared.Tag;
using Content.Shared.VendingMachines;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lua.Achievements;

public sealed partial class AchievementTriggerSystem
{
    private static readonly ProtoId<TagPrototype> HardsuitTag = "Hardsuit";
    private static readonly ProtoId<TagPrototype> OreTag = "Ore";
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextSpaceWalkCheck;

    partial void InitializeBulkTriggers()
    {
        SubscribeLocalEvent<MindComponent, RoleAddedEvent>(OnRoleAdded);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<VendingMachineComponent, BoundUIOpenedEvent>(OnVendingOpened);
        SubscribeLocalEvent<ResearchConsoleComponent, BoundUIOpenedEvent>(OnResearchOpened);
        SubscribeLocalEvent<LatheComponent, LatheStartPrintingEvent>(OnLathePrint);
        SubscribeLocalEvent<BuckleComponent, BuckledEvent>(OnBuckled);
        SubscribeLocalEvent<EmagSuccessEvent>(OnEmagSuccess);
        SubscribeLocalEvent<PullerComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<MetaDataComponent, UseInHandEvent>(OnItemUsedInHand, after: [typeof(ExpendableLightSystem)]);
        SubscribeLocalEvent<InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PaperTextSavedEvent>(OnPaperTextSaved);
        SubscribeLocalEvent<FaxSentEvent>(OnFaxSent);
        SubscribeLocalEvent<JetpackEnabledEvent>(OnJetpackEnabled);
        SubscribeLocalEvent<StatusEffectsComponent, StatusEffectAddedEvent>(OnStatusEffectAdded);
        SubscribeLocalEvent<InjectorInjectedEvent>(OnInjectorInjected);
        SubscribeLocalEvent<GasTankInternalsConnectedEvent>(OnGasTankInternalsConnected);
        SubscribeLocalEvent<BibleHealAttemptEvent>(OnBibleHealAttempt);
        SubscribeLocalEvent<ClonedMindAddedEvent>(OnClonedMindAdded);
        SubscribeLocalEvent<HandsComponent, DidEquipHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<TriggerEvent>(OnTriggered);
        SubscribeNetworkEvent<InstrumentStartMidiEvent>(OnInstrumentStartMidi);
        SubscribeLocalEvent<CarryStartedEvent>(OnCarryStarted);
        SubscribeLocalEvent<MicrowaveCookStartedEvent>(OnMicrowaveCookStarted);
        SubscribeLocalEvent<ChemistryMachineUiOpenedEvent>(OnChemistryMachineUiOpened);
        SubscribeLocalEvent<RCDOperationCompletedEvent>(OnRcdOperationCompleted);
        SubscribeLocalEvent<CryostorageEnteredEvent>(OnCryostorageEntered);
    }

    partial void UpdateBulk(float frameTime)
    {
        if (_timing.CurTime < _nextSpaceWalkCheck)
            return;

        _nextSpaceWalkCheck = _timing.CurTime + TimeSpan.FromSeconds(1.5);

        var query = EntityQueryEnumerator<ActorComponent, InventoryComponent, TransformComponent, BarotraumaComponent>();
        while (query.MoveNext(out var uid, out _, out var inventory, out _, out _))
        {
            if (!_inventory.TryGetSlotEntity(uid, "outerClothing", out var suit, inventory)) continue;
            if (!_tags.HasTag(suit.Value, HardsuitTag)) continue;
            var mixture = _atmosphere.GetContainingMixture(uid);
            var pressure = mixture?.Pressure ?? 0f;
            if (pressure > Atmospherics.WarningLowPressure) continue;
            TryUnlockPlayer(uid, AchievementIds.MiscSpaceWalk);
        }
    }

    private void OnRoleAdded(Entity<MindComponent> ent, ref RoleAddedEvent args)
    {
        if (ent.Comp.CurrentEntity is not { Valid: true } entity || !HasComp<ActorComponent>(entity)) return;
        if (!_players.TryGetSessionByEntity(entity, out var session)) return;
        if (_roles.MindIsAntagonist(ent)) TryUnlockSession(session, AchievementIds.MiscAntag);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!HasComp<GhostComponent>(args.Entity)) return;
        TryUnlockSession(args.Player, AchievementIds.TutorialGhost);
    }

    private void OnVendingOpened(EntityUid uid, VendingMachineComponent _, BoundUIOpenedEvent args)
    {
        if (!args.UiKey.Equals(VendingMachineUiKey.Key))
            return;

        TryUnlockPlayer(args.Actor, AchievementIds.TutorialVending);
    }

    private void OnResearchOpened(EntityUid uid, ResearchConsoleComponent _, BoundUIOpenedEvent args)
    {
        if (!args.UiKey.Equals(ResearchConsoleUiKey.Key))
            return;

        TryUnlockPlayer(args.Actor, AchievementIds.TutorialResearch);
    }

    private void OnLathePrint(Entity<LatheComponent> ent, ref LatheStartPrintingEvent args)
    {
        if (!TryComp<LatheProducingComponent>(ent, out var producing) || producing.Actor is not { Valid: true } actor)
            return;

        TryUnlockPlayer(actor, AchievementIds.TutorialLathe);
    }

    private void OnBuckled(Entity<BuckleComponent> ent, ref BuckledEvent args)
    {
        if (!HasComp<ActorComponent>(ent))
            return;

        TryUnlockPlayer(ent, AchievementIds.TutorialBuckle);
    }

    private void OnEmagSuccess(EmagSuccessEvent args)
    {
        TryUnlockPlayer(args.User, AchievementIds.MiscEmag);
    }

    private void OnPullStarted(EntityUid uid, PullerComponent _, PullStartedMessage msg)
    {
        if (msg.PullerUid != uid || !HasComp<ActorComponent>(uid))
            return;

        TryUnlockPlayer(uid, AchievementIds.MiscPull);
    }

    private void OnCarryStarted(ref CarryStartedEvent args)
    {
        if (!HasComp<ActorComponent>(args.Carrier))
            return;

        TryUnlockPlayer(args.Carrier, AchievementIds.MiscCarry);
    }

    private void OnMicrowaveCookStarted(ref MicrowaveCookStartedEvent args)
    {
        if (args.User is not { Valid: true } user || !HasComp<ActorComponent>(user))
            return;

        TryUnlockPlayer(user, AchievementIds.TutorialCook);
    }

    private void OnChemistryMachineUiOpened(ref ChemistryMachineUiOpenedEvent args)
    {
        if (args.User is not { Valid: true } user || !HasComp<ActorComponent>(user))
            return;

        var proto = MetaData(args.Machine).EntityPrototype?.ID;
        if (proto == null)
            return;

        if (proto.Contains("ChemDispenser", StringComparison.OrdinalIgnoreCase) ||
            proto.Contains("ChemMaster", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(user, AchievementIds.TutorialChemistry);
    }

    private void OnRcdOperationCompleted(ref RCDOperationCompletedEvent args)
    {
        if (!HasComp<ActorComponent>(args.User))
            return;

        TryUnlockPlayer(args.User, AchievementIds.TutorialRCD);
    }

    private void OnCryostorageEntered(ref CryostorageEnteredEvent args)
    {
        TryUnlockPlayer(args.User, AchievementIds.TutorialCryo);
    }

    private void OnItemUsedInHand(EntityUid uid, MetaDataComponent meta, UseInHandEvent args)
    {
        if (args.User is not { Valid: true } user)
            return;

        var usedProto = meta.EntityPrototype?.ID;
        if (usedProto == null)
            return;
        if (usedProto.Contains("DrinkWaterBottle", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(user, AchievementIds.Thirsty);
        if (usedProto.StartsWith("FoodPSBBar", StringComparison.Ordinal))
            TryUnlockPlayer(user, AchievementIds.SnackBreak);
        if (usedProto.Contains("BikeHorn", StringComparison.OrdinalIgnoreCase) ||
            usedProto.Contains("CluwneHorn", StringComparison.OrdinalIgnoreCase) ||
            usedProto.Contains("PushHorn", StringComparison.OrdinalIgnoreCase) ||
            usedProto.Contains("BananiumHorn", StringComparison.OrdinalIgnoreCase) ||
            usedProto.Contains("Honk", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(user, AchievementIds.MiscHonk);
    }

    private void OnInjectorInjected(ref InjectorInjectedEvent args)
    {
        if (!HasComp<ActorComponent>(args.User))
            return;

        var proto = MetaData(args.Injector).EntityPrototype?.ID;
        if (proto == null)
            return;

        if (proto.Contains("SpaceMedipen", StringComparison.OrdinalIgnoreCase) && args.User == args.Target)
            TryUnlockPlayer(args.User, AchievementIds.NeedFreshAir);

        if (proto.Contains("EmergencyMedipen", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(args.User, AchievementIds.ImADoctor);
    }

    private void OnGasTankInternalsConnected(ref GasTankInternalsConnectedEvent args)
    {
        var beneficiary = args.User ?? args.InternalsOwner;
        TryUnlockPlayer(beneficiary, AchievementIds.TutorialInternals);
    }

    private void OnBibleHealAttempt(ref BibleHealAttemptEvent args)
    {
        TryUnlockPlayer(args.User, AchievementIds.MiscBible);
    }

    private void OnClonedMindAdded(ref ClonedMindAddedEvent args)
    {
        if (!HasComp<ActorComponent>(args.CloneUid))
            return;

        TryUnlockPlayer(args.CloneUid, AchievementIds.MiscCloned);
    }

    private void OnFaxSent(ref FaxSentEvent args)
    {
        TryUnlockPlayer(args.User, AchievementIds.MiscFax);
    }

    private void OnJetpackEnabled(ref JetpackEnabledEvent args)
    {
        TryUnlockPlayer(args.User, AchievementIds.MiscJetpack);
    }

    private void OnPaperTextSaved(ref PaperTextSavedEvent args)
    {
        TryUnlockPlayer(args.User, AchievementIds.MiscPaper);
    }

    private void OnHandEquipped(EntityUid uid, HandsComponent _, DidEquipHandEvent args)
    {
        if (!HasComp<ActorComponent>(uid))
            return;

        var proto = MetaData(args.Equipped).EntityPrototype?.ID;
        if (proto == null)
            return;

        if (proto.Contains("Suppermatter", StringComparison.OrdinalIgnoreCase) ||
            proto.Contains("Supermatter", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(uid, AchievementIds.MiscSupermatter);

        if (_tags.HasTag(args.Equipped, OreTag))
            TryUnlockPlayer(uid, AchievementIds.TutorialMining);
    }

    private void OnInstrumentStartMidi(InstrumentStartMidiEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } player)
            return;

        var uid = GetEntity(msg.Uid);

        if (!TryComp(uid, out InstrumentComponent? instrument))
            return;

        if (args.SenderSession.AttachedEntity != instrument.InstrumentPlayer)
            return;

        TryUnlockPlayer(player, AchievementIds.MiscInstrument);
    }

    private void OnTriggered(TriggerEvent args)
    {
        if (args.User is not { Valid: true } user)
            return;

        if (!HasComp<ExplosiveComponent>(args.Triggered) && !HasComp<ExplodeOnTriggerComponent>(args.Triggered))
            return;

        TryUnlockPlayer(user, AchievementIds.MiscBomb);
    }

    private void OnInteractUsing(InteractUsingEvent args)
    {
        if (args.User is not { Valid: true } user)
            return;

        if (HasComp<HealthAnalyzerComponent>(args.Used))
            TryUnlockPlayer(user, AchievementIds.TutorialHealthAnalyzer);

        if (HasComp<HandcuffComponent>(args.Used))
            TryUnlockPlayer(user, AchievementIds.TutorialHandcuffs);

        var usedProto = MetaData(args.Used).EntityPrototype?.ID;
        if (usedProto == null)
            return;

        if (usedProto.Contains("Welder", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(user, AchievementIds.TutorialWelder);
        if (usedProto.Contains("Forensic", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(user, AchievementIds.MiscForensic);
        if (usedProto.Contains("Seeds", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(user, AchievementIds.TutorialPlant);
        if (usedProto.Contains("Extinguisher", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(user, AchievementIds.TutorialFireExtinguisher);
        if (usedProto.Contains("Crowbar", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(user, AchievementIds.TutorialCrowbar);
        if (usedProto.Contains("Defibrillator", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(user, AchievementIds.TutorialDefib);
    }

    private void OnStatusEffectAdded(Entity<StatusEffectsComponent> ent, ref StatusEffectAddedEvent args)
    {
        if (!HasComp<ActorComponent>(ent) || args.Key != SharedDrunkSystem.DrunkKey.Id)
            return;

        TryUnlockPlayer(ent, AchievementIds.MiscDrunk);
    }
}
