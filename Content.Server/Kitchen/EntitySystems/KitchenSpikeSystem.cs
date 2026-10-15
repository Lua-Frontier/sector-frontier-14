using Content.Server.Administration.Logs;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Kitchen.Components;
using Content.Server.Popups;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Kitchen.EntitySystems
{
    public sealed class KitchenSpikeSystem : SharedKitchenSpikeSystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly IAdminLogManager _logger = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly BodySystem _bodySystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedMindSystem _mind = default!;
        [Dependency] private readonly MetaDataSystem _metaData = default!;
        [Dependency] private readonly SharedSuicideSystem _suicide = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<KitchenSpikeComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
            SubscribeLocalEvent<KitchenSpikeComponent, EntInsertedIntoContainerMessage>(OnEntInsertedIntoContainer);
            SubscribeLocalEvent<KitchenSpikeComponent, EntRemovedFromContainerMessage>(OnEntRemovedFromContainer);
            SubscribeLocalEvent<KitchenSpikeComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<KitchenSpikeComponent, InteractHandEvent>(OnInteractHand);
            SubscribeLocalEvent<KitchenSpikeComponent, DragDropTargetEvent>(OnDragDrop);
            SubscribeLocalEvent<KitchenSpikeComponent, ExaminedEvent>(OnSpikeExamined);
            SubscribeLocalEvent<KitchenSpikeComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
            SubscribeLocalEvent<KitchenSpikeComponent, DestructionEventArgs>(OnDestruction);
            SubscribeLocalEvent<KitchenSpikeComponent, EntityTerminatingEvent>(OnTerminating);

            //DoAfter
            SubscribeLocalEvent<KitchenSpikeComponent, SpikeHookDoAfterEvent>(OnSpikeHookDoAfter);
            SubscribeLocalEvent<KitchenSpikeComponent, SpikeUnhookDoAfterEvent>(OnSpikeUnhookDoAfter);
            SubscribeLocalEvent<KitchenSpikeComponent, SpikeButcherDoAfterEvent>(OnSpikeButcherDoAfter);

            SubscribeLocalEvent<KitchenSpikeComponent, SuicideByEnvironmentEvent>(OnSuicideByEnvironment);

            SubscribeLocalEvent<KitchenSpikeVictimComponent, ExaminedEvent>(OnVictimExamined);
            SubscribeLocalEvent<KitchenSpikeVictimComponent, MobStateChangedEvent>(OnVictimMobStateChanged);

            SubscribeLocalEvent<ButcherableComponent, CanDropDraggedEvent>(OnButcherableCanDrop);
        }

        private void OnButcherableCanDrop(Entity<ButcherableComponent> entity, ref CanDropDraggedEvent args)
        {
            args.Handled = true;
            args.CanDrop |= entity.Comp.Type != ButcheringType.Knife;
        }

        /// <summary>
        /// TODO: Update this so it actually meatspikes the user instead of applying lethal damage to them.
        /// </summary>
        private void OnSuicideByEnvironment(Entity<KitchenSpikeComponent> entity, ref SuicideByEnvironmentEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<DamageableComponent>(args.Victim, out var damageableComponent))
                return;

            _suicide.ApplyLethalDamage((args.Victim, damageableComponent), "Piercing");
            var othersMessage = Loc.GetString("comp-kitchen-spike-suicide-other",
                                                ("victim", Identity.Entity(args.Victim, EntityManager)),
                                                ("this", entity));
            _popupSystem.PopupEntity(othersMessage, args.Victim, Filter.PvsExcept(args.Victim), true);

            var selfMessage = Loc.GetString("comp-kitchen-spike-suicide-self",
                                            ("this", entity));
            _popupSystem.PopupEntity(selfMessage, args.Victim, args.Victim);
            args.Handled = true;
        }

        private void OnInsertAttempt(Entity<KitchenSpikeComponent> ent, ref ContainerIsInsertingAttemptEvent args)
        {
            if (args.Cancelled || args.Container.ID != ent.Comp.ContainerId)
                return;

            if (!TryComp<ButcherableComponent>(args.EntityUid, out var butcherable) || butcherable.Type != ButcheringType.Spike)
                args.Cancel();
        }

        private void OnEntInsertedIntoContainer(Entity<KitchenSpikeComponent> ent, ref EntInsertedIntoContainerMessage args)
        {
            if (args.Container.ID != ent.Comp.ContainerId)
                return;

            EnsureComp<KitchenSpikeHookedComponent>(args.Entity);
            _damageableSystem.TryChangeDamage(args.Entity, ent.Comp.SpikeDamage, true);

            ent.Comp.NextDamage = _gameTiming.CurTime + ent.Comp.DamageInterval;
        }

        private void OnEntRemovedFromContainer(Entity<KitchenSpikeComponent> ent, ref EntRemovedFromContainerMessage args)
        {
            if (args.Container.ID != ent.Comp.ContainerId)
                return;

            if (!TerminatingOrDeleted(args.Entity))
            {
                RemComp<KitchenSpikeHookedComponent>(args.Entity);
                _damageableSystem.TryChangeDamage(args.Entity, ent.Comp.SpikeDamage, true);
            }
        }

        private void OnInteractHand(Entity<KitchenSpikeComponent> ent, ref InteractHandEvent args)
        {
            if (args.Handled || !ent.Comp.BodyContainer.ContainedEntity.HasValue)
                return;

            _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-knife-needed"), ent, args.User);
            args.Handled = true;
        }

        private void OnInteractUsing(Entity<KitchenSpikeComponent> ent, ref InteractUsingEvent args)
        {
            var victim = ent.Comp.BodyContainer.ContainedEntity;

            if (args.Handled || !TryComp<ButcherableComponent>(victim, out var butcherable))
                return;

            args.Handled = true;

            if (butcherable.SpawnedEntities.Count == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-butcher-empty", ("victim", Identity.Entity(victim.Value, EntityManager))), ent, args.User, PopupType.MediumCaution);
                return;
            }

            if (!TryComp<SharpComponent>(args.Used, out var sharp))
            {
                _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-knife-needed"), ent, args.User);
                return;
            }

            var victimIdentity = Identity.Entity(victim.Value, EntityManager);

            _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-begin-butcher-self", ("victim", victimIdentity)), ent, args.User, PopupType.MediumCaution);
            _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-begin-butcher", ("user", Identity.Entity(args.User, EntityManager)), ("victim", victimIdentity)),
                ent,
                Filter.PvsExcept(args.User),
                true,
                PopupType.MediumCaution);

            var delay = TimeSpan.FromSeconds(butcherable.ButcherDelay * sharp.ButcherDelayModifier);

            if (_mobStateSystem.IsAlive(victim.Value))
                delay += ent.Comp.ButcherDelayAlive;
            else
                delay *= ent.Comp.ButcherModifierDead;

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
                args.User,
                delay,
                new SpikeButcherDoAfterEvent(),
                ent,
                target: victim,
                used: args.Used)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                NeedHand = true,
            });
        }

        private void OnDragDrop(Entity<KitchenSpikeComponent> ent, ref DragDropTargetEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;

            if (!Spikeable(ent, args.User, args.Dragged))
                return;

            ShowPopups("comp-kitchen-spike-begin-hook-self",
                "comp-kitchen-spike-begin-hook-self-other",
                "comp-kitchen-spike-begin-hook-other-self",
                "comp-kitchen-spike-begin-hook-other",
                args.User,
                args.Dragged,
                ent);

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
                args.User,
                ent.Comp.HookDelay,
                new SpikeHookDoAfterEvent(),
                ent,
                target: args.Dragged)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                NeedHand = true,
            });
        }

        private void OnSpikeHookDoAfter(Entity<KitchenSpikeComponent> ent, ref SpikeHookDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || !args.Target.HasValue)
                return;

            args.Handled = true;

            if (!Spikeable(ent, args.User, args.Target.Value))
                return;

            if (!ContainerSystem.Insert(args.Target.Value, ent.Comp.BodyContainer))
                return;

            ShowPopups("comp-kitchen-spike-hook-self",
                "comp-kitchen-spike-hook-self-other",
                "comp-kitchen-spike-hook-other-self",
                "comp-kitchen-spike-hook-other",
                args.User,
                args.Target.Value,
                ent);

            // normally medium severity, but for humanoids high severity, so new players get relay'd to admin alerts.
            var logSeverity = HasComp<HumanoidAppearanceComponent>(args.Target) ? LogImpact.High : LogImpact.Medium;

            _logger.Add(LogType.Action,
                logSeverity,
                $"{ToPrettyString(args.User):user} put {ToPrettyString(args.Target):target} on the {ToPrettyString(ent):spike}");

            _audio.PlayPvs(ent.Comp.SpikeSound, ent);
        }

        private void OnSpikeUnhookDoAfter(Entity<KitchenSpikeComponent> ent, ref SpikeUnhookDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || !args.Target.HasValue)
                return;

            args.Handled = true;

            if (!ContainerSystem.Remove(args.Target.Value, ent.Comp.BodyContainer))
                return;

            ShowPopups("comp-kitchen-spike-unhook-self",
                "comp-kitchen-spike-unhook-self-other",
                "comp-kitchen-spike-unhook-other-self",
                "comp-kitchen-spike-unhook-other",
                args.User,
                args.Target.Value,
                ent);

            _logger.Add(LogType.Action,
                LogImpact.Medium,
                $"{ToPrettyString(args.User):user} took {ToPrettyString(args.Target):target} off the {ToPrettyString(ent):spike}");

            _audio.PlayPvs(ent.Comp.SpikeSound, ent);
        }

        private void OnSpikeButcherDoAfter(Entity<KitchenSpikeComponent> ent, ref SpikeButcherDoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || !args.Target.HasValue || !TryComp<ButcherableComponent>(args.Target, out var butcherable))
                return;

            var victim = args.Target.Value;

            if (ent.Comp.BodyContainer.ContainedEntity != victim || butcherable.SpawnedEntities.Count == 0)
                return;

            args.Handled = true;

            var victimIdentity = Identity.Entity(victim, EntityManager);

            _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-butcher-self", ("victim", victimIdentity)), ent, args.User, PopupType.MediumCaution);
            _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-butcher", ("user", Identity.Entity(args.User, EntityManager)), ("victim", victimIdentity)),
                ent,
                Filter.PvsExcept(args.User),
                true,
                PopupType.MediumCaution);

            // Get a random entry to spawn.
            var index = _random.Next(butcherable.SpawnedEntities.Count);
            var entry = butcherable.SpawnedEntities[index];

            if (entry.PrototypeId != null && _random.Prob(entry.SpawnProbability))
            {
                var uid = SpawnNextToOrDrop(entry.PrototypeId.Value, ent);
                _metaData.SetEntityName(uid,
                    Loc.GetString("comp-kitchen-spike-meat-name",
                        ("name", Name(uid)),
                        ("victim", victim)));
            }

            // Decrease the amount since we spawned an entity from that entry.
            entry.Amount--;

            // Remove the entry if its new amount is zero, or update it.
            if (entry.Amount <= 0)
                butcherable.SpawnedEntities.RemoveAt(index);
            else
                butcherable.SpawnedEntities[index] = entry;

            var humanoid = HasComp<HumanoidAppearanceComponent>(victim);

            // Gib the victim if there is nothing else to butcher.
            if (butcherable.SpawnedEntities.Count == 0 && (!humanoid || ent.Comp.GibHumanoids))
            {
                _logger.Add(LogType.Gib,
                    humanoid ? LogImpact.Extreme : LogImpact.High,
                    $"{ToPrettyString(args.User):user} finished butchering {ToPrettyString(victim):target} on the {ToPrettyString(ent):spike}");

                GibVictim(ent, victim, humanoid);
            }
            else
            {
                EnsureComp<KitchenSpikeVictimComponent>(victim);

                _damageableSystem.TryChangeDamage(victim, ent.Comp.ButcherDamage, true);

                // Log severity for damaging other entities is normally medium.
                _logger.Add(LogType.Action,
                    LogImpact.Medium,
                    $"{ToPrettyString(args.User):user} butchered {ToPrettyString(victim):target} on the {ToPrettyString(ent):spike}");
            }

            _audio.PlayPvs(ent.Comp.ButcherSound, ent);
        }

        private void GibVictim(Entity<KitchenSpikeComponent> ent, EntityUid victim, bool humanoid)
        {
            ContainerSystem.Remove(victim, ent.Comp.BodyContainer, force: true, destination: Transform(ent).Coordinates);

            if (!TryComp<BodyComponent>(victim, out var body))
            {
                QueueDel(victim);
                return;
            }

            var gibs = _bodySystem.GibBody(victim, true, body);

            if (humanoid)
                return;

            foreach (var gib in gibs)
            {
                if (HasComp<BrainComponent>(gib) && _mind.TryGetMind(gib, out _, out _))
                    continue;

                QueueDel(gib);
            }
        }

        private void OnSpikeExamined(Entity<KitchenSpikeComponent> ent, ref ExaminedEvent args)
        {
            var victim = ent.Comp.BodyContainer.ContainedEntity;

            if (!victim.HasValue)
                return;

            // Show it at the end of the examine so it looks good.
            args.PushMarkup(Loc.GetString("comp-kitchen-spike-hooked", ("victim", Identity.Entity(victim.Value, EntityManager))), -1);
            args.PushMessage(_examineSystem.GetExamineText(victim.Value, args.Examiner), -2);
        }

        private void OnGetVerbs(Entity<KitchenSpikeComponent> ent, ref GetVerbsEvent<Verb> args)
        {
            var victim = ent.Comp.BodyContainer.ContainedEntity;

            if (!args.CanAccess || !args.CanInteract || !victim.HasValue)
                return;

            var user = args.User;

            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString("comp-kitchen-spike-unhook-verb"),
                Act = () => TryUnhook(ent, user, victim.Value),
                Impact = LogImpact.Medium,
            });
        }

        private void OnDestruction(Entity<KitchenSpikeComponent> ent, ref DestructionEventArgs args)
        {
            ContainerSystem.EmptyContainer(ent.Comp.BodyContainer, true, Transform(ent).Coordinates);
        }

        private void OnTerminating(Entity<KitchenSpikeComponent> ent, ref EntityTerminatingEvent args)
        {
            var xform = Transform(ent);

            if (TerminatingOrDeleted(xform.ParentUid))
                return;

            ContainerSystem.EmptyContainer(ent.Comp.BodyContainer, true, xform.Coordinates);
        }

        private void OnVictimExamined(Entity<KitchenSpikeVictimComponent> ent, ref ExaminedEvent args)
        {
            args.PushMarkup(Loc.GetString("comp-kitchen-spike-victim-examine", ("target", Identity.Entity(ent, EntityManager))));
        }

        private void OnVictimMobStateChanged(Entity<KitchenSpikeVictimComponent> ent, ref MobStateChangedEvent args)
        {
            if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
                RemCompDeferred<KitchenSpikeVictimComponent>(ent);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<KitchenSpikeComponent>();

            while (query.MoveNext(out var uid, out var kitchenSpike))
            {
                var contained = kitchenSpike.BodyContainer.ContainedEntity;

                if (!contained.HasValue)
                    continue;

                if (kitchenSpike.NextDamage > _gameTiming.CurTime)
                    continue;

                kitchenSpike.NextDamage += kitchenSpike.DamageInterval;

                if (_mobStateSystem.IsDead(contained.Value))
                    continue;

                _damageableSystem.TryChangeDamage(contained.Value, kitchenSpike.TimeDamage, true);
            }
        }

        /// <summary>
        /// A helper method to show popups that can be targeted towards yourself or somebody else.
        /// </summary>
        private void ShowPopups(string selfLocMessageSelf,
            string selfLocMessageOthers,
            string locMessageSelf,
            string locMessageOthers,
            EntityUid user,
            EntityUid victim,
            EntityUid hook)
        {
            string messageSelf, messageOthers;

            var victimIdentity = Identity.Entity(victim, EntityManager);

            if (user == victim)
            {
                messageSelf = Loc.GetString(selfLocMessageSelf, ("hook", hook));
                messageOthers = Loc.GetString(selfLocMessageOthers, ("victim", victimIdentity), ("hook", hook));
            }
            else
            {
                messageSelf = Loc.GetString(locMessageSelf, ("victim", victimIdentity), ("hook", hook));
                messageOthers = Loc.GetString(locMessageOthers,
                    ("user", Identity.Entity(user, EntityManager)),
                    ("victim", victimIdentity),
                    ("hook", hook));
            }

            _popupSystem.PopupEntity(messageSelf, hook, user, PopupType.MediumCaution);
            _popupSystem.PopupEntity(messageOthers, hook, Filter.PvsExcept(user), true, PopupType.MediumCaution);
        }

        /// <summary>
        /// Tries to unhook the victim.
        /// </summary>
        private void TryUnhook(Entity<KitchenSpikeComponent> ent, EntityUid user, EntityUid target)
        {
            ShowPopups("comp-kitchen-spike-begin-unhook-self",
                "comp-kitchen-spike-begin-unhook-self-other",
                "comp-kitchen-spike-begin-unhook-other-self",
                "comp-kitchen-spike-begin-unhook-other",
                user,
                target,
                ent);

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
                user,
                ent.Comp.UnhookDelay,
                new SpikeUnhookDoAfterEvent(),
                ent,
                target: target)
            {
                BreakOnDamage = user != target,
                BreakOnMove = true,
            });
        }

        private bool Spikeable(Entity<KitchenSpikeComponent> ent, EntityUid userUid, EntityUid victimUid)
        {
            if (ent.Comp.BodyContainer.ContainedEntity.HasValue)
            {
                _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-deny-occupied", ("this", ent.Owner)), ent, userUid);
                return false;
            }

            if (!TryComp<ButcherableComponent>(victimUid, out var butcherable))
            {
                _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-deny-butcher", ("victim", Identity.Entity(victimUid, EntityManager)), ("this", ent.Owner)), victimUid, userUid);
                return false;
            }

            switch (butcherable.Type)
            {
                case ButcheringType.Spike:
                    break;
                case ButcheringType.Knife:
                    _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-deny-butcher-knife", ("victim", Identity.Entity(victimUid, EntityManager)), ("this", ent.Owner)), victimUid, userUid);
                    return false;
                default:
                    _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-deny-butcher", ("victim", Identity.Entity(victimUid, EntityManager)), ("this", ent.Owner)), victimUid, userUid);
                    return false;
            }

            if (ent.Comp.RequireDead && TryComp<MobStateComponent>(victimUid, out var mobState) && !_mobStateSystem.IsDead(victimUid, mobState))
            {
                _popupSystem.PopupEntity(Loc.GetString("comp-kitchen-spike-deny-not-dead", ("victim", Identity.Entity(victimUid, EntityManager))),
                    victimUid, userUid);
                return false;
            }

            return true;
        }
    }
}
