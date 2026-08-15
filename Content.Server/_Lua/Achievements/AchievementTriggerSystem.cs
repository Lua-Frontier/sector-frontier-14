// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.NPC.HTN;
using Content.Server._Mono.Company;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Systems;
using Content.Server.Tesla.Components;
using Content.Shared._Goobstation.SpaceWhale;
using Content.Shared._Mono.Company;
using Content.Shared._Lua.Achievements;
using Content.Shared._Lua.DonateShop;
using Content.Shared._Lua.Expedition;
using Content.Shared._Lua.JumpAbility;
using Content.Shared._Lua.Sprint;
using Content.Shared.Anomaly.Components;
using Content.Shared.Singularity.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Bed.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Clothing;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Light.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Power;
using Content.Shared.Roles;
using Content.Shared.Shuttles.Events;
using Content.Shared.Speech;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Lua.Achievements;

public sealed partial class AchievementTriggerSystem : EntitySystem
{
    [Dependency] private readonly AchievementSystem _achievements = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly AchievementUnlockRegistry _unlockRegistry = new();
    private readonly Dictionary<EntityUid, EntityUid> _lastKillOrigins = new();
    private float _jobPlaytimeAccum;
    private const float JobPlaytimeCheckInterval = 15f;
    private const float SpaceWhaleKillCreditRadius = 2000f;

    public override void Initialize()
    {
        base.Initialize();

        RebuildUnlockRegistry();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, after: [typeof(CompanySystem)]);
        SubscribeLocalEvent<CompanySetEvent>(OnCompanySet);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<FTLStartedEvent>(OnExpeditionDepart);
        SubscribeLocalEvent<ActorComponent, MoveInputEvent>(OnSprintInput);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageTaken);
        SubscribeLocalEvent<StaminaComponent, BeforeStaminaDamageEvent>(OnStaminaDamage);
        SubscribeLocalEvent<SleepingComponent, SleepStateChangedEvent>(OnSleepChanged);
        SubscribeLocalEvent<IgnitionSourceComponent, UseInHandEvent>(OnFlareUsed, after: [typeof(ExpendableLightSystem)]);
        SubscribeLocalEvent<LuaDirectionalJumpEvent>(OnJump);
        SubscribeLocalEvent<ScreamActionEvent>(OnScream);
        SubscribeLocalEvent<ToggleCombatActionEvent>(OnCombatToggle);
        SubscribeLocalEvent<ActionComponent, ActionPerformedEvent>(OnActionPerformed);
        SubscribeNetworkEvent<RequestDonateShopOpenMessage>(OnDonateShopOpen);
        SubscribeLocalEvent<BatteryInterfaceComponent, BoundUIOpenedEvent>(OnBatteryUiOpened);
        SubscribeLocalEvent<InventoryComponent, DidUnequipEvent>(OnUnequipped);
        SubscribeLocalEvent<InventoryComponent, DidEquipEvent>(OnEquipped);
        SubscribeLocalEvent<ThrusterDisabledByUserEvent>(OnThrusterDisabledByUser);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<AnomalyComponent, ExaminedEvent>(OnAnomalyExamined);
        SubscribeLocalEvent<SingularityComponent, ExaminedEvent>(OnSingularityExamined);
        SubscribeLocalEvent<TargetDefibrillatedEvent>(OnTargetDefibrillated);
        SubscribeLocalEvent<TeslaEnergyBallComponent, InteractHandEvent>(OnTeslaInteract);

        InitializeBulkTriggers();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AchievementPrototype>())
            RebuildUnlockRegistry();
    }

    private void RebuildUnlockRegistry()
    {
        _unlockRegistry.Build(_prototypes);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        _ = CheckPlaytimeAchievements(args.Player);
        TryUnlockSpecies(args.Player, args.Profile.Species);
        TryUnlockCompanyFromComponent(args.Mob, args.Player);
        CheckJobAchievements(args);
    }

    private void OnCompanySet(CompanySetEvent args)
    {
        if (!args.Changed || !HasComp<ActorComponent>(args.Entity))
            return;

        if (!_players.TryGetSessionByEntity(args.Entity, out var session))
            return;

        TryUnlockCompany(session, args.NewCompanyId);
    }

    private void TryUnlockSpecies(ICommonSession session, string species)
    {
        if (!_unlockRegistry.BySpecies.TryGetValue(species, out var ids))
            return;

        foreach (var id in ids)
            _ = _achievements.TryUnlockAsync(session, id);
    }

    private void TryUnlockCompanyFromComponent(EntityUid mob, ICommonSession session)
    {
        if (!TryComp<CompanyComponent>(mob, out var company))
            return;

        TryUnlockCompany(session, company.CompanyName);
    }

    private void TryUnlockCompany(ICommonSession session, string companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            companyId = "None";

        if (!_unlockRegistry.ByCompany.TryGetValue(companyId, out var ids))
            return;

        foreach (var id in ids)
            _ = _achievements.TryUnlockAsync(session, id);
    }

    private void CheckJobAchievements(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null)
            return;

        var job = args.JobId;
        if (string.Equals(job, "StationRepresentative", StringComparison.OrdinalIgnoreCase))
            TryUnlockSession(args.Player, AchievementIds.MiscCaptain);
        else if (string.Equals(job, "Paramedic", StringComparison.OrdinalIgnoreCase))
            TryUnlockSession(args.Player, AchievementIds.MiscMedical);
        else if (job.Contains("Engineer", StringComparison.OrdinalIgnoreCase) || job.Contains("Atmos", StringComparison.OrdinalIgnoreCase))
            TryUnlockSession(args.Player, AchievementIds.MiscEngineer);
        else if (job.Contains("Security", StringComparison.OrdinalIgnoreCase) || job.Contains("Officer", StringComparison.OrdinalIgnoreCase) || job.Contains("Deputy", StringComparison.OrdinalIgnoreCase))
            TryUnlockSession(args.Player, AchievementIds.MiscSecurityJob);
    }

    private void TryUnlockSession(ICommonSession session, string achievementId)
    {
        _ = _achievements.TryUnlockAsync(session, achievementId);
    }

    private void OnRoundEnd(RoundEndMessageEvent ev)
    {
        var seen = new HashSet<NetUserId>();

        foreach (var info in ev.AllPlayersEndInfo)
        {
            if (info.PlayerGuid is not { } userId || !seen.Add(userId))
                continue;

            _ = _achievements.TryUnlockByUserIdAsync(userId, AchievementIds.FirstShift);
        }
    }

    private async Task CheckPlaytimeAchievements(ICommonSession session)
    {
        _playTime.FlushTracker(session);
        var overall = _playTime.GetOverallPlaytime(session);

        foreach (var (hours, id) in AchievementPlaytimeTiers.All)
        {
            if (overall.TotalHours < hours)
                break;

            await _achievements.TryUnlockAsync(session, id);
        }

        await CheckJobRoleAchievements(session);
    }

    private async Task CheckJobRoleAchievements(ICommonSession session)
    {
        if (!_playTime.TryGetTrackerTimes(session, out var times))
            return;

        foreach (var (id, jobId) in _unlockRegistry.JobAvailable)
        {
            if (!_prototypes.TryIndex<JobPrototype>(jobId, out var job))
                continue;

            if (!AchievementJobText.JobPlaytimeRequirementsMet(job, EntityManager, _prototypes, times))
                continue;

            await _achievements.TryUnlockAsync(session, id);
        }

        foreach (var (id, jobId, hours) in _unlockRegistry.JobPlayed)
        {
            if (!_prototypes.TryIndex<JobPrototype>(jobId, out var job))
                continue;

            if (!times.TryGetValue(job.PlayTimeTracker, out var played))
                played = TimeSpan.Zero;

            if (played < TimeSpan.FromHours(hours))
                continue;

            await _achievements.TryUnlockAsync(session, id);
        }
    }

    private void OnExpeditionDepart(ref FTLStartedEvent ev)
    {
        var ftlQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (ftlQuery.MoveNext(out var ftlUid, out _, out var ftlXform))
        {
            if (ftlXform.GridUid == ev.Entity)
                TryUnlockPlayer(ftlUid, AchievementIds.MiscFtl);
        }

        if (ev.FromMapUid is not { } fromMap || !TryComp<ExpeditionMapComponent>(fromMap, out var expedition))
            return;

        if (!TryComp(ev.Entity, out TransformComponent? shuttleXform))
            return;

        if (_station.GetOwningStation(ev.Entity, shuttleXform) != expedition.Station)
            return;
        var crewQuery = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (crewQuery.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (xform.GridUid != ev.Entity)
                continue;

            if (!_mobState.IsAlive(uid, mobState))
                continue;

            TryUnlockPlayer(uid, AchievementIds.SeenItAll);
        }
    }

    private void OnSprintInput(Entity<ActorComponent> ent, ref MoveInputEvent args)
    {
        if (!HasComp<LuaSprintComponent>(ent))
            return;

        var wasSprinting = (args.OldMovement & MoveButtons.Sprint) != 0;
        if (!TryComp<InputMoverComponent>(ent, out var mover) || !mover.IsSprinting || wasSprinting)
            return;

        TryUnlockPlayer(ent, AchievementIds.SprintRun);
    }

    private void OnDamageTaken(EntityUid uid, DamageableComponent _, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (args.Origin is { Valid: true } origin &&
            TryResolveKiller(origin) is { Valid: true } killer)
        {
            _lastKillOrigins[uid] = killer;
        }

        if (!HasComp<ActorComponent>(uid))
            return;

        TryUnlockPlayer(uid, AchievementIds.Ouch);

        if (args.Origin is not { Valid: true } damageOrigin)
            return;

        var originProto = MetaData(damageOrigin).EntityPrototype?.ID;
        if (originProto != null && originProto.Contains("Supermatter", StringComparison.OrdinalIgnoreCase))
            TryUnlockPlayer(uid, AchievementIds.MiscSupermatter);
    }

    private void OnStaminaDamage(Entity<StaminaComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (args.Value <= 0f || !HasComp<ActorComponent>(ent))
            return;

        TryUnlockPlayer(ent, AchievementIds.StaminaBad);
    }

    private void OnSleepChanged(Entity<SleepingComponent> ent, ref SleepStateChangedEvent args)
    {
        if (!args.FellAsleep || !HasComp<ActorComponent>(ent))
            return;

        if (!TryComp<BuckleComponent>(ent, out var buckle) || buckle.BuckledTo is not { } bed)
            return;

        if (!HasComp<HealOnBuckleComponent>(bed) && !HasComp<StasisBedComponent>(bed))
            return;

        TryUnlockPlayer(ent, AchievementIds.SleepToys);
    }

    private void OnFlareUsed(Entity<IgnitionSourceComponent> ent, ref UseInHandEvent args)
    {
        if (!args.Handled || MetaData(ent).EntityPrototype?.ID != "Flare")
            return;

        if (!TryComp<ExpendableLightComponent>(ent, out var light) || light.CurrentState != ExpendableLightState.Lit)
            return;

        TryUnlockPlayer(args.User, AchievementIds.DarkHere);
    }

    private void OnJump(LuaDirectionalJumpEvent args)
    {
        TryUnlockPlayer(args.Performer, AchievementIds.BigJump);
    }

    private void OnScream(ScreamActionEvent args)
    {
        TryUnlockPlayer(args.Performer, AchievementIds.Aaaaa);
    }

    private void OnCombatToggle(ToggleCombatActionEvent args)
    {
        TryUnlockPlayer(args.Performer, AchievementIds.WannaFight);
    }

    private void OnActionPerformed(Entity<ActionComponent> ent, ref ActionPerformedEvent args)
    {
        if (MetaData(ent).EntityPrototype?.ID != "ActionToggleLight")
            return;

        TryUnlockPlayer(args.Performer, AchievementIds.PdaLight);
    }

    private void OnDonateShopOpen(RequestDonateShopOpenMessage msg, EntitySessionEventArgs args)
    {
        _ = _achievements.TryUnlockAsync(args.SenderSession, AchievementIds.JustLooking);
    }

    private void OnBatteryUiOpened(EntityUid uid, BatteryInterfaceComponent _, BoundUIOpenedEvent args)
    {
        if (!args.UiKey.Equals(BatteryUiKey.Key))
            return;

        var protoId = MetaData(uid).EntityPrototype?.ID;
        if (protoId == null || !protoId.Contains("SMES", StringComparison.OrdinalIgnoreCase))
            return;

        TryUnlockPlayer(args.Actor, AchievementIds.SmesConfused);
    }

    private void OnUnequipped(EntityUid uid, InventoryComponent _, DidUnequipEvent args)
    {
        if (!HasComp<ActorComponent>(uid))
            return;

        if (args.Slot != "jumpsuit")
            return;

        TryUnlockPlayer(uid, AchievementIds.DontLook);
    }

    private void OnEquipped(EntityUid uid, InventoryComponent _, DidEquipEvent args)
    {
        if (!HasComp<ActorComponent>(uid))
            return;
        if (args.Slot == "outerClothing" && _tags.HasTag(args.Equipment, "Hardsuit"))
            TryUnlockPlayer(uid, AchievementIds.HardsuitTime);

        if (args.Slot == "mask")
            TryUnlockPlayer(uid, AchievementIds.MiscMask);
        if (args.Slot == "ears")
            TryUnlockPlayer(uid, AchievementIds.MiscHeadset);
        if (args.Slot == "belt")
            TryUnlockPlayer(uid, AchievementIds.MiscToolbelt);
        if (args.Slot == "shoes" && HasComp<MagbootsComponent>(args.Equipment))
            TryUnlockPlayer(uid, AchievementIds.MiscMagboots);
    }

    private void OnThrusterDisabledByUser(ThrusterDisabledByUserEvent args)
    {
        TryUnlockPlayer(args.User, AchievementIds.Warm);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        var ent = args.Target;

        if (args.OldMobState == MobState.Critical && args.NewMobState == MobState.Alive)
        {
            if (HasComp<ActorComponent>(ent))
                TryUnlockPlayer(ent, AchievementIds.MiscRecoverCritical);

            if (args.Origin is { Valid: true } origin && origin != ent)
            {
                if (TryResolveReviver(origin) is { Valid: true } reviver)
                    TryUnlockPlayer(reviver, AchievementIds.MiscReviveOther);
            }
        }

        if (HasComp<ActorComponent>(ent))
        {
            if (args.NewMobState == MobState.Critical)
                TryUnlockPlayer(ent, AchievementIds.NotTheEnd);

            if (args.NewMobState == MobState.Dead)
                TryUnlockPlayer(ent, AchievementIds.TheEnd);

            return;
        }

        if (args.NewMobState != MobState.Dead)
            return;

        if (HasComp<SpaceWhaleComponent>(ent))
        {
            CreditNearbySpaceWhaleKillers(ent);
            return;
        }

        EntityUid killer;
        if (TryResolveKiller(args.Origin) is { Valid: true } resolved)
            killer = resolved;
        else if (!_lastKillOrigins.TryGetValue(ent, out killer))
            return;

        TryUnlockKillAchievements(ent, killer);
        _lastKillOrigins.Remove(ent);

        if (HasComp<HTNComponent>(ent))
            TryUnlockPlayer(killer, AchievementIds.LeaveMeAlone);
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        var uid = args.Entity.Owner;

        if (HasComp<ActorComponent>(uid))
        {
            _lastKillOrigins.Remove(uid);
            return;
        }

        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
        {
            _lastKillOrigins.Remove(uid);
            return;
        }

        if (!_lastKillOrigins.Remove(uid, out var killer) || !HasComp<ActorComponent>(killer))
            return;

        TryUnlockKillAchievements(uid, killer);

        if (HasComp<HTNComponent>(uid))
            TryUnlockPlayer(killer, AchievementIds.LeaveMeAlone);
    }

    private void TryUnlockKillAchievements(EntityUid victim, EntityUid killer)
    {
        if (!_players.TryGetSessionByEntity(killer, out var session))
            return;

        var protoId = MetaData(victim).EntityPrototype?.ID;
        foreach (var achievementId in _unlockRegistry.MatchKill(protoId))
            _ = _achievements.AddKillProgressAsync(session, achievementId);
    }

    private EntityUid? TryResolveKiller(EntityUid? origin)
    {
        if (origin is not { Valid: true } uid)
            return null;

        if (HasComp<ActorComponent>(uid))
            return uid;

        return TryResolveReviver(uid);
    }

    private void OnSingularityExamined(Entity<SingularityComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ActorComponent>(args.Examiner))
            return;

        TryUnlockPlayer(args.Examiner, AchievementIds.MiscSingularity);
    }

    private void OnTargetDefibrillated(ref TargetDefibrillatedEvent args)
    {
        if (!HasComp<ActorComponent>(args.User))
            return;

        TryUnlockPlayer(args.User, AchievementIds.MiscReviveOther);
    }

    private EntityUid? TryResolveReviver(EntityUid origin)
    {
        if (HasComp<ActorComponent>(origin))
            return origin;

        var query = EntityQueryEnumerator<ActorComponent, HandsComponent>();
        while (query.MoveNext(out var holder, out _, out var hands))
        {
            Entity<HandsComponent?> holderEnt = (holder, hands);
            if (_hands.IsHolding(holderEnt, origin))
                return holder;
        }

        return null;
    }

    private void OnAnomalyExamined(Entity<AnomalyComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ActorComponent>(args.Examiner))
            return;

        TryUnlockPlayer(args.Examiner, AchievementIds.MiscAnomaly);
    }

    private void OnTeslaInteract(EntityUid uid, TeslaEnergyBallComponent comp, InteractHandEvent args)
    {
        if (!HasComp<ActorComponent>(args.User))
            return;

        TryUnlockPlayer(args.User, AchievementIds.SoftEmbrace);
    }

    private void CreditNearbySpaceWhaleKillers(EntityUid whale)
    {
        if (!TryComp<TransformComponent>(whale, out var whaleXform))
            return;

        var whalePos = _transform.GetWorldPosition(whaleXform);
        var whaleMap = whaleXform.MapID;
        var range2 = SpaceWhaleKillCreditRadius * SpaceWhaleKillCreditRadius;

        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var playerUid, out _, out var mobState, out var playerXform))
        {
            if (!_mobState.IsAlive(playerUid, mobState))
                continue;

            if (playerXform.MapID != whaleMap)
                continue;

            var d2 = (_transform.GetWorldPosition(playerXform) - whalePos).LengthSquared();
            if (d2 > range2)
                continue;

            CreditSpaceWhaleKill(playerUid);
        }
    }

    private void CreditSpaceWhaleKill(EntityUid player)
    {
        if (!_players.TryGetSessionByEntity(player, out var session))
            return;

        _ = _achievements.AddKillProgressAsync(session, AchievementIds.MiscSpaceWhale);
    }

    partial void InitializeBulkTriggers();
    partial void UpdateBulk(float frameTime);

    public override void Update(float frameTime)
    {
        UpdateBulk(frameTime);

        _jobPlaytimeAccum += frameTime;
        if (_jobPlaytimeAccum < JobPlaytimeCheckInterval)
            return;

        _jobPlaytimeAccum = 0f;

        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out _, out var actor))
        {
            if (!_playTime.TryGetTrackerTimes(actor.PlayerSession, out _))
                continue;

            _playTime.FlushTracker(actor.PlayerSession);
            _ = CheckJobRoleAchievements(actor.PlayerSession);
        }
    }

    private void TryUnlockPlayer(EntityUid player, string achievementId)
    {
        if (!_players.TryGetSessionByEntity(player, out var session))
            return;

        _ = _achievements.TryUnlockAsync(session, achievementId);
    }
}
