using Content.Shared._Lua.Autodoc;
using Content.Shared._Lua.Autodoc.Components;
using Content.Shared._Shitmed.Autodoc.Components;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DragDrop;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Shared._Shitmed.Autodoc.Systems;

public abstract class SharedAutodocSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly LabelSystem _label = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedSurgerySystem _surgery = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutodocComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<AutodocComponent, EntInsertedIntoContainerMessage>(OnBodyInserted);
        SubscribeLocalEvent<AutodocComponent, EntRemovedFromContainerMessage>(OnBodyRemoved);
        SubscribeLocalEvent<AutodocComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
        SubscribeLocalEvent<AutodocComponent, GetVerbsEvent<InteractionVerb>>(AddInsertOtherVerb);
        SubscribeLocalEvent<AutodocComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<AutodocComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<AutodocComponent, CanDropTargetEvent>(OnCanDragDropOn);
        SubscribeLocalEvent<AutodocComponent, DragDropTargetEvent>(OnDragDropOn);
        SubscribeLocalEvent<AutodocComponent, BoundUserInterfaceCheckRangeEvent>(OnUiRangeCheck);
        SubscribeLocalEvent<AutodocComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<AutodocComponent, AutodocCaptureItemEvent>(OnCaptureItem);

        Subs.BuiEvents<AutodocComponent>(AutodocUiKey.Key, s =>
        {
            s.Event<BoundUIOpenedEvent>(OnUiOpened);
            s.Event<AutodocSelectPartMessage>(OnSelectPart);
            s.Event<AutodocRemovePartMessage>(OnRemovePart);
            s.Event<AutodocHealPartMessage>(OnHealPart);
            s.Event<AutodocTransferMessage>(OnTransfer);
            s.Event<AutodocStopMessage>(OnStop);
        });

        SubscribeLocalEvent<ActiveAutodocComponent, SurgeryStepEvent>(OnSurgeryStep);
        SubscribeLocalEvent<ActiveAutodocComponent, SurgeryStepFailedEvent>(OnSurgeryStepFailed);
        SubscribeLocalEvent<ActiveAutodocComponent, ComponentShutdown>(OnActiveShutdown);
    }

    private void OnComponentInit(Entity<AutodocComponent> ent, ref ComponentInit args)
    {
        EnsureBodyContainer(ent);
        UpdateAppearance(ent);
    }

    private void OnBodyInserted(Entity<AutodocComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.BodyContainerId)
            return;

        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    private void OnBodyRemoved(Entity<AutodocComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.BodyContainerId)
            return;

        // Ejecting the patient aborts any running program.
        if (IsActive(ent))
            RemCompDeferred<ActiveAutodocComponent>(ent);

        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    private void OnRelayMovement(Entity<AutodocComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        if (!_blocker.CanInteract(args.Entity, ent))
            return;

        EjectBody(ent);
    }

    private void AddInsertOtherVerb(Entity<AutodocComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (args.Using == null ||
            !args.CanAccess ||
            !args.CanInteract ||
            IsOccupied(ent) ||
            !CanInsert(ent, args.Using.Value))
            return;

        var toInsert = args.Using.Value;
        var name = Identity.Name(toInsert, EntityManager);
        InteractionVerb verb = new()
        {
            Act = () => InsertBody(ent, toInsert),
            Category = VerbCategory.Insert,
            Text = name
        };
        args.Verbs.Add(verb);
    }

    private void AddAlternativeVerbs(Entity<AutodocComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        EnsureBodyContainer(ent);
        var isOccupant = ent.Comp.BodyContainer?.ContainedEntity == user;

        // Occupant is inside the machine and usually fails CanAccess; still allow UI + eject for them.
        if ((!args.CanAccess || !args.CanInteract) && !isOccupant)
            return;

        if (!args.CanInteract && !isOccupant)
            return;

        if (IsOccupied(ent) && (args.CanAccess || isOccupant))
        {
            AlternativeVerb eject = new()
            {
                Act = () => EjectBody(ent),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("autodoc-verb-noun-occupant"),
                Priority = 1
            };
            args.Verbs.Add(eject);
        }

        if (isOccupant)
        {
            AlternativeVerb openUi = new()
            {
                Act = () => _ui.TryOpenUi(ent.Owner, AutodocUiKey.Key, user),
                Text = Loc.GetString("autodoc-verb-open-ui"),
                Priority = 2
            };
            args.Verbs.Add(openUi);
            return;
        }

        if (!IsOccupied(ent) &&
            args.CanAccess &&
            CanInsert(ent, user) &&
            _blocker.CanMove(user))
        {
            AlternativeVerb enter = new()
            {
                Act = () => InsertBody(ent, user),
                Text = Loc.GetString("autodoc-verb-enter")
            };
            args.Verbs.Add(enter);
        }
    }

    private void OnPowerChanged(Entity<AutodocComponent> ent, ref PowerChangedEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnDestroyed(Entity<AutodocComponent> ent, ref DestructionEventArgs args)
    {
        EjectBody(ent);
    }

    private void OnCanDragDropOn(Entity<AutodocComponent> ent, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= CanInsert(ent, args.Dragged);
    }

    private void OnDragDropOn(Entity<AutodocComponent> ent, ref DragDropTargetEvent args)
    {
        InsertBody(ent, args.Dragged);
        args.Handled = true;
    }

    private void OnUiRangeCheck(Entity<AutodocComponent> ent, ref BoundUserInterfaceCheckRangeEvent args)
    {
        EnsureBodyContainer(ent);
        if (ent.Comp.BodyContainer?.ContainedEntity == args.Actor.Owner)
            args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    #region UI Handling

    private void OnUiOpened(Entity<AutodocComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSelectPart(Entity<AutodocComponent> ent, ref AutodocSelectPartMessage args)
    {
        if (GetEntity(args.Part) is not { Valid: true } part || !IsPatientPart(ent, part))
            return;

        ent.Comp.SelectedPart = part;
        UpdateUi(ent);
    }

    private void OnRemovePart(Entity<AutodocComponent> ent, ref AutodocRemovePartMessage args)
    {
        if (GetEntity(args.Part) is not { Valid: true } part || !IsPatientPart(ent, part))
            return;

        StartOperation(ent, AutodocOperationKind.RemovePart, part, new List<EntProtoId> { "SurgeryRemovePart" }, args.Actor);
    }

    private void OnHealPart(Entity<AutodocComponent> ent, ref AutodocHealPartMessage args)
    {
        if (GetEntity(args.Part) is not { Valid: true } part || !IsPatientPart(ent, part))
            return;

        var patient = GetPatient(ent);
        if (patient == null)
            return;

        var surgeries = new List<EntProtoId>();
        if (HasTendWoundsDamage(patient.Value, part, SurgeryTendWoundsBrute))
            surgeries.Add(SurgeryTendWoundsBrute);
        if (HasTendWoundsDamage(patient.Value, part, SurgeryTendWoundsBurn))
            surgeries.Add(SurgeryTendWoundsBurn);

        if (surgeries.Count != 0)
            StartOperation(ent, AutodocOperationKind.TendWounds, part, surgeries, args.Actor);
    }

    private void OnTransfer(Entity<AutodocComponent> ent, ref AutodocTransferMessage args)
    {
        if (IsActive(ent) || GetEntity(args.Item) is not { Valid: true } item)
            return;

        if (args.Source == AutodocTransferTarget.BodyPart && args.Destination == AutodocTransferTarget.Storage)
        {
            if (IsPatientPart(ent, item))
                StartOperation(ent, AutodocOperationKind.RemovePart, item, new List<EntProtoId> { "SurgeryRemovePart" }, args.Actor);
            return;
        }

        if (args.Source == AutodocTransferTarget.OrganSlot && args.Destination == AutodocTransferTarget.Storage)
        {
            if (!TryComp<OrganComponent>(item, out var organ) || !TryFindOrganPart(ent, item, out var part) ||
                GetOrganSurgery(organ.SlotId, insert: false) is not { } surgery)
                return;

            StartOperation(ent, AutodocOperationKind.RemoveOrgan, part, new List<EntProtoId> { surgery }, args.Actor, item);
            return;
        }

        if (args.Source != AutodocTransferTarget.Storage ||
            !TryComp<StorageComponent>(ent, out var storage) ||
            !storage.Container.Contains(item) ||
            !TryComp<HandsComponent>(ent, out var hands))
            return;

        if (args.Destination == AutodocTransferTarget.BodyPart && TryComp<BodyPartComponent>(item, out var bodyPart))
        {
            if (GetAttachSurgery(bodyPart.PartType, bodyPart.Symmetry) is not { } surgery ||
                FindAttachTarget(ent, bodyPart.PartType, bodyPart.Symmetry) is not { } target)
                return;

            if (TryGetOccupiedBodyPart(ent, bodyPart.PartType, bodyPart.Symmetry) is not null)
            {
                RejectTransfer(ent, "part-already-present");
                return;
            }

            if (!GrabItem((ent.Owner, ent.Comp, hands), item))
                return;

            StartOperation(ent, AutodocOperationKind.AttachPart, target, new List<EntProtoId> { surgery }, args.Actor, item);
            return;
        }

        if (args.Destination == AutodocTransferTarget.OrganSlot &&
            TryComp<OrganComponent>(item, out var storedOrgan) &&
            args.TargetPart is { } netPart &&
            GetEntity(netPart) is { Valid: true } targetPart &&
            IsPatientPart(ent, targetPart) &&
            GetOrganSurgery(args.OrganSlot ?? storedOrgan.SlotId, insert: true) is { } organSurgery)
        {
            var organSlot = args.OrganSlot ?? storedOrgan.SlotId;
            if (TryGetOccupiedOrgan(targetPart, organSlot) is not null)
            {
                RejectTransfer(ent, "organ-already-present");
                return;
            }

            if (!GrabItem((ent.Owner, ent.Comp, hands), item))
                return;

            StartOperation(ent, AutodocOperationKind.AttachOrgan, targetPart, new List<EntProtoId> { organSurgery }, args.Actor, item);
        }
    }

    private void RejectTransfer(Entity<AutodocComponent> ent, string errorKey)
    {
        Say(ent, Loc.GetString("autodoc-error", ("error", Loc.GetString("autodoc-error-" + errorKey))));
        UpdateUi(ent);
    }

    private EntityUid? TryGetOccupiedBodyPart(Entity<AutodocComponent> ent, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (GetPatient(ent) is not { } patient)
            return null;

        foreach (var (partId, _) in _body.GetBodyChildrenOfType(patient, type, symmetry: symmetry))
        {
            if (!HasComp<BodyPartReattachedComponent>(partId))
                return partId;
        }
        return null;
    }

    private EntityUid? TryGetOccupiedOrgan(EntityUid part, string slotId)
    {
        foreach (var (organId, organ) in _body.GetPartOrgans(part))
        {
            if (organ.SlotId != slotId)
                continue;

            if (!HasComp<OrganReattachedComponent>(organId))
                return organId;
        }

        return null;
    }

    private void OnStop(Entity<AutodocComponent> ent, ref AutodocStopMessage args)
    {
        RemComp<ActiveAutodocComponent>(ent);
        UpdateUi(ent);
    }

    #endregion

    private void OnSurgeryStep(Entity<ActiveAutodocComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryComp<AutodocComponent>(ent, out _))
            return;

        if (HasComp<SurgeryRepeatableStepComponent>(args.Step)
            && TryComp<SurgeryTendWoundsEffectComponent>(args.Step, out var tend)
            && !args.Complete
            && !HasTendWoundsDamageForGroup(args.Body, args.Part, tend.MainGroup))
        {
            ent.Comp.Waiting = false;
            return;
        }

        var repeatable = HasComp<SurgeryRepeatableStepComponent>(args.Step);
        if (args.Complete || !repeatable)
        {
            ent.Comp.CompletedSteps++;
            ent.Comp.Waiting = false; // try the next autodoc or surgery step
            if (TryComp<AutodocComponent>(ent, out var autodoc))
                UpdateUi((ent.Owner, autodoc));
            return;
        }

        // for tend wounds dont abort, more wounds need tending
        if (HasComp<SurgeryRepeatableStepComponent>(args.Step))
            return;

        ent.Comp.Waiting = repeatable;
    }

    private void OnSurgeryStepFailed(Entity<ActiveAutodocComponent> ent, ref SurgeryStepFailedEvent args)
    {
        if (ent.Comp.SuppressSurgeryFailureEvent)
            return;

        if (!TryComp<AutodocComponent>(ent, out var comp))
            return;

        ent.Comp.Waiting = false;
        if (IsOperationGoalComplete((ent.Owner, comp, ent.Comp)))
        {
            ent.Comp.CurrentSurgery = null;
            ent.Comp.SurgeryIndex = ent.Comp.Surgeries.Count;
        }
        else
        {
            ent.Comp.Failed = true;
        }

        UpdateUi((ent.Owner, comp));
    }

    private void OnActiveShutdown(Entity<ActiveAutodocComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<AutodocComponent>(ent, out var comp))
            return;

        // wake the patient when program completes or errors out
        if (GetPatient((ent.Owner, comp)) is { } patient)
            WakePatient(patient);

        if (TryComp<HandsComponent>(ent, out var hands))
        {
            try
            {
                StoreItemOrThrow((ent.Owner, comp, hands));
            }
            catch (AutodocError error)
            {
                Say(ent, Loc.GetString("autodoc-error", ("error", Loc.GetString("autodoc-error-" + error.Message))));
            }
        }

        UpdateUi((ent.Owner, comp), forceInactive: true);
    }

    protected virtual void WakePatient(EntityUid patient)
    {
        _sleeping.TryWaking(patient);
    }

    protected virtual float GetPatientTemperature(EntityUid patient) => float.NaN;

    #region Body Slot

    private void EnsureBodyContainer(Entity<AutodocComponent> ent)
    {
        ent.Comp.BodyContainer ??= _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.BodyContainerId);
    }

    public bool IsOccupied(Entity<AutodocComponent> ent)
    {
        EnsureBodyContainer(ent);
        return ent.Comp.BodyContainer!.ContainedEntity != null;
    }

    public bool CanInsert(Entity<AutodocComponent> ent, EntityUid target)
    {
        return !IsOccupied(ent) && HasComp<BodyComponent>(target);
    }

    public bool InsertBody(Entity<AutodocComponent> ent, EntityUid target)
    {
        if (!CanInsert(ent, target))
            return false;

        EnsureBodyContainer(ent);

        if (TryComp(target, out BuckleComponent? buckle) && buckle.Buckled)
            _buckle.TryUnbuckle(target, target, buckle);

        var xform = Transform(target);
        if (!_container.Insert((target, xform), ent.Comp.BodyContainer!))
            return false;

        UpdateAppearance(ent);
        return true;
    }

    public void EjectBody(Entity<AutodocComponent> ent)
    {
        EnsureBodyContainer(ent);
        if (ent.Comp.BodyContainer!.ContainedEntity is not { Valid: true } contained)
            return;

        _container.Remove(contained, ent.Comp.BodyContainer);
        _climb.ForciblySetClimbing(contained, ent);
        UpdateAppearance(ent);
    }

    public void UpdateAppearance(Entity<AutodocComponent> ent)
    {
        EnsureBodyContainer(ent);

        var status = !_power.IsPowered(ent.Owner)
            ? AutodocStatus.Off
            : IsOccupied(ent)
                ? AutodocStatus.Occupied
                : AutodocStatus.Open;

        _appearance.SetData(ent, AutodocVisuals.Status, status);
    }

    #endregion

    #region Step API

    public bool IsSurgery(EntProtoId id)
    {
        // this is O(n) so with a fuck ton of surgeries it could slow down the server
        return _surgery.AllSurgeries.Contains(id);
    }

    public EntityUid? FindItem(EntityUid uid, string name)
    {
        var storage = Comp<StorageComponent>(uid);
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (Name(item) == name)
                return item;
        }

        return null;
    }

    public EntityUid? FindItem(EntityUid uid, EntityWhitelist? whitelist)
    {
        var storage = Comp<StorageComponent>(uid);
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (_whitelist.IsWhitelistPassOrNull(whitelist, item))
                return item;
        }

        return null;
    }

    private void OnCaptureItem(Entity<AutodocComponent> ent, ref AutodocCaptureItemEvent args)
    {
        TryCaptureRemovedItem(ent, args.Item);
    }

    /// <summary>
    /// Picks up a freshly removed organ or body part for the autodoc program to store or label.
    /// Organs go to the surgery hand; body parts go straight to storage because they lack <see cref="ItemComponent"/>.
    /// </summary>
    public bool TryCaptureRemovedItem(EntityUid autodoc, EntityUid item)
    {
        var comp = Comp<AutodocComponent>(autodoc);

        if (HasComp<BodyPartComponent>(item))
            return TryComp<StorageComponent>(autodoc, out var partStorage)
                && _storage.Insert(autodoc, item, out _, storageComp: partStorage);

        if (TryComp<HandsComponent>(autodoc, out var hands)
            && _hands.TryPickup(autodoc, item, comp.ItemSlot, checkActionBlocker: false, animate: false, handsComp: hands))
            return true;

        return TryComp<StorageComponent>(autodoc, out var storage)
            && _storage.Insert(autodoc, item, out _, storageComp: storage);
    }

    public bool GrabItem(Entity<AutodocComponent, HandsComponent> ent, EntityUid item)
    {
        return _hands.TryPickup(ent, item, ent.Comp1.ItemSlot, checkActionBlocker: false, animate: false, handsComp: ent.Comp2);
    }

    public void GrabItemOrThrow(Entity<AutodocComponent, HandsComponent> ent, EntityUid item)
    {
        if (!GrabItem(ent, item))
            throw new AutodocError("hand-full");
    }

    public void StoreItemOrThrow(Entity<AutodocComponent, HandsComponent> ent)
    {
        if (_hands.GetHeldItem((ent.Owner, ent.Comp2), ent.Comp1.ItemSlot) is not { } item)
            return; // Body parts are inserted directly during capture.

        if (!_storage.Insert(ent, item, out _))
            throw new AutodocError("storage-full");
    }

    public EntityUid GetHeldOrThrow(Entity<AutodocComponent, HandsComponent> ent)
    {
        if (TryGetOperatedItem(ent) is not { } item)
            throw new AutodocError("item-unavailable");

        return item;
    }

    /// <summary>
    /// Returns the item currently held for surgery, or a body part already placed in storage during capture.
    /// </summary>
    public EntityUid? TryGetOperatedItem(Entity<AutodocComponent, HandsComponent> ent)
    {
        if (_hands.GetHeldItem((ent.Owner, ent.Comp2), ent.Comp1.ItemSlot) is { } held)
            return held;

        if (!TryComp<StorageComponent>(ent, out var storage))
            return null;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (HasComp<BodyPartComponent>(item))
                return item;
        }

        return null;
    }

    public void LabelItem(EntityUid item, string label)
    {
        _label.Label(item, label);
    }

    public void DelayUpdate(EntityUid uid, TimeSpan delay)
    {
        if (TryComp<ActiveAutodocComponent>(uid, out var active))
            active.NextUpdate += delay;
    }

    public EntityUid? GetPatient(Entity<AutodocComponent> ent)
    {
        EnsureBodyContainer(ent);
        if (ent.Comp.BodyContainer!.ContainedEntity is not { } patient)
            return null;

        if (!HasComp<SurgeryTargetComponent>(patient))
            return null;

        return patient;
    }

    public EntityUid GetPatientOrThrow(Entity<AutodocComponent> ent)
    {
        if (GetPatient(ent) is not { } patient)
            throw new AutodocError("missing-patient");

        return patient;
    }

    public EntityUid? FindPart(EntityUid patient, BodyPartType type, BodyPartSymmetry? symmetry)
    {
        foreach (var ent in _body.GetBodyChildrenOfType(patient, type, symmetry: symmetry))
        {
            return ent.Id;
        }

        return null;
    }

    /// <summary>
    /// Starts doing a surgery, throwing if it fails.
    /// Returns true if there is no next step, i.e. the surgery is done.
    /// </summary>
    public bool StartSurgeryOrThrow(Entity<AutodocComponent> ent, EntityUid patient, EntityUid part, EntProtoId surgery)
    {
        if (_surgery.GetSingleton(surgery) is not { } singleton)
            throw new AutodocError("reality-breaking");

        if (_surgery.GetNextStep(patient, part, singleton) is not { } pair)
            return false;

        var nextSurgery = pair.Item1;
        if (MetaData(nextSurgery).EntityPrototype?.ID is not { } surgeryId) // should never happen
            throw new AutodocError("reality-breaking");

        var index = pair.Item2;
        var nextStep = nextSurgery.Comp.Steps[index];
        if (!TryDoSurgeryStep(ent, patient, part, surgeryId, nextStep, out var error))
        {
            if (TryComp<ActiveAutodocComponent>(ent, out var active) &&
                IsOperationGoalComplete((ent.Owner, ent.Comp, active)))
                return false;

            // if the omnitool is held inserting organ etc will fail
            // may need to swap hands to the selected item instead of omnitool
            // if that works then it'll swap back automatically for the next step
            if (error != StepInvalidReason.MissingTool && error != StepInvalidReason.ToolInvalid)
                throw new AutodocError($"step-invalid-{error}");

            TrySwapAutodocHand(ent);
            if (!TryDoSurgeryStep(ent, patient, part, surgeryId, nextStep, out error))
                throw new AutodocError($"step-invalid-{error}"); // no trying again just fail
        }

        var comp = Comp<ActiveAutodocComponent>(ent);
        comp.CurrentSurgery = (patient, part, surgery);
        comp.Waiting = true; // don't go onto next step until doafter finishes
        return true;
    }

    private bool TryDoSurgeryStep(
        EntityUid autodoc,
        EntityUid patient,
        EntityUid part,
        EntProtoId surgery,
        EntProtoId step,
        out StepInvalidReason error)
    {
        var active = Comp<ActiveAutodocComponent>(autodoc);
        active.SuppressSurgeryFailureEvent = true;
        try
        {
            return _surgery.TryDoSurgeryStep(patient, part, autodoc, surgery, step, out error);
        }
        finally
        {
            active.SuppressSurgeryFailureEvent = false;
        }
    }

    private void TrySwapAutodocHand(EntityUid uid)
    {
        // EnumerateHands yields active hand first, then the rest.
        var skipActive = true;
        foreach (var hand in _hands.EnumerateHands(uid))
        {
            if (skipActive)
            {
                skipActive = false;
                continue;
            }

            _hands.TrySetActiveHand(uid, hand);
            return;
        }
    }

    public bool IsActive(EntityUid uid)
    {
        return HasComp<ActiveAutodocComponent>(uid);
    }

    private bool StartOperation(
        Entity<AutodocComponent> ent,
        AutodocOperationKind operation,
        EntityUid targetPart,
        List<EntProtoId> surgeries,
        EntityUid user,
        EntityUid? item = null)
    {
        if (IsActive(ent) || surgeries.Count == 0 || GetPatient(ent) is not { } patient)
            return false;

        var active = EnsureComp<ActiveAutodocComponent>(ent);
        active.Operation = operation;
        active.TargetPart = targetPart;
        active.Item = item;
        active.Surgeries = surgeries;
        active.TotalSteps = Math.Max(1, surgeries.Sum(surgery => CountSurgerySteps(surgery, new HashSet<EntProtoId>())));
        active.NextUpdate = Timing.CurTime + ent.Comp.UpdateDelay;

        _adminLogger.Add(LogType.InteractActivate, LogImpact.High,
            $"{ToPrettyString(user):user} started autodoc operation {operation} on {ToPrettyString(patient):patient}");
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    /// Removes a step from a program, returning true if it succeeded.
    /// </summary>
    public bool Proceed(Entity<AutodocComponent, ActiveAutodocComponent> ent)
    {
        if (ent.Comp2.Waiting)
            return false;

        if (ent.Comp2.Failed)
        {
            var error = Loc.GetString("autodoc-error-surgery-failed");
            Say(ent, Loc.GetString("autodoc-fatal-error", ("error", error)));
            return true;
        }

        if (IsOperationGoalComplete(ent))
        {
            Say(ent, Loc.GetString("autodoc-operation-completed"));
            return true;
        }

        try
        {
            if (ent.Comp2.CurrentSurgery is { } args)
            {
                var (body, part, currentSurgery) = args;
                if (IsTendWoundsSurgery(currentSurgery) && !HasTendWoundsDamage(body, part, currentSurgery))
                {
                    ent.Comp2.CurrentSurgery = null;
                    ent.Comp2.SurgeryIndex++;
                    UpdateUi((ent.Owner, ent.Comp1));
                    return false;
                }

                if (StartSurgeryOrThrow((ent.Owner, ent.Comp1), body, part, currentSurgery))
                    return false;

                ent.Comp2.CurrentSurgery = null;
                ent.Comp2.SurgeryIndex++;
                UpdateUi((ent.Owner, ent.Comp1));
            }

            if (ent.Comp2.SurgeryIndex >= ent.Comp2.Surgeries.Count)
            {
                Say(ent, Loc.GetString("autodoc-operation-completed"));
                return true;
            }

            var patient = GetPatientOrThrow((ent.Owner, ent.Comp1));
            var surgery = ent.Comp2.Surgeries[ent.Comp2.SurgeryIndex];
            if (IsTendWoundsSurgery(surgery) && !HasTendWoundsDamage(patient, ent.Comp2.TargetPart, surgery))
            {
                ent.Comp2.SurgeryIndex++;
                return false;
            }

            if (!StartSurgeryOrThrow((ent.Owner, ent.Comp1), patient, ent.Comp2.TargetPart, surgery))
                ent.Comp2.SurgeryIndex++;
        }
        catch (AutodocError e)
        {
            var error = Loc.GetString("autodoc-error-" + e.Message);
            Say(ent, Loc.GetString("autodoc-fatal-error", ("error", error)));
            return true;
        }

        UpdateUi((ent.Owner, ent.Comp1));
        return false;
    }

    private static readonly EntProtoId SurgeryTendWoundsBrute = "SurgeryTendWoundsBrute";
    private static readonly EntProtoId SurgeryTendWoundsBurn = "SurgeryTendWoundsBurn";
    private static readonly string[] BruteDamageTypes = { "Slash", "Blunt", "Piercing" };
    private static readonly string[] BurnDamageTypes = { "Heat", "Shock", "Cold", "Caustic" };

    public bool IsTendWoundsSurgery(EntProtoId surgery) =>
        surgery == SurgeryTendWoundsBrute || surgery == SurgeryTendWoundsBurn;

    public bool HasTendWoundsDamage(EntityUid patient, EntityUid part, EntProtoId surgery)
    {
        var group = surgery == SurgeryTendWoundsBrute
            ? BruteDamageTypes
            : surgery == SurgeryTendWoundsBurn
                ? BurnDamageTypes
                : null;

        return group != null && HasTendWoundsDamageForGroup(patient, part, group);
    }

    private bool HasTendWoundsDamageForGroup(EntityUid body, EntityUid part, string mainGroup)
    {
        var group = mainGroup == "Brute" ? BruteDamageTypes : BurnDamageTypes;
        return HasTendWoundsDamageForGroup(body, part, group);
    }

    private bool HasTendWoundsDamageForGroup(EntityUid body, EntityUid part, string[] group)
    {
        if (TryComp<DamageableComponent>(body, out var bodyDamage) && HasDamageInGroup(bodyDamage, group))
            return true;

        return TryComp<DamageableComponent>(part, out var partDamage) && HasDamageInGroup(partDamage, group);
    }

    private static bool HasDamageInGroup(DamageableComponent damageable, string[] group)
    {
        foreach (var damageType in group)
        {
            if (damageable.Damage.DamageDict.TryGetValue(damageType, out var value) && value > 0)
                return true;
        }

        return false;
    }

    private bool IsPatientPart(Entity<AutodocComponent> ent, EntityUid part)
    {
        return GetPatient(ent) is { } patient &&
               TryComp<BodyPartComponent>(part, out var partComp) &&
               partComp.Body == patient;
    }

    private bool IsOperationGoalComplete(Entity<AutodocComponent, ActiveAutodocComponent> ent)
    {
        var patient = GetPatient((ent.Owner, ent.Comp1));
        if (patient == null)
            return false;

        return ent.Comp2.Operation switch
        {
            AutodocOperationKind.AttachPart => ent.Comp2.Item is { } part &&
                TryComp<BodyPartComponent>(part, out var partComp) && partComp.Body == patient &&
                !HasComp<BodyPartReattachedComponent>(part),
            AutodocOperationKind.RemovePart => TryComp<BodyPartComponent>(ent.Comp2.TargetPart, out var removedPartComp) &&
                removedPartComp.Body != patient,
            AutodocOperationKind.AttachOrgan => ent.Comp2.Item is { } organ &&
                TryComp<OrganComponent>(organ, out var organComp) && organComp.Body == patient &&
                !HasComp<OrganReattachedComponent>(organ),
            AutodocOperationKind.RemoveOrgan => ent.Comp2.Item is { } removedOrgan &&
                TryComp<OrganComponent>(removedOrgan, out var removedOrganComp) && removedOrganComp.Body != patient,
            AutodocOperationKind.TendWounds =>
                !HasTendWoundsDamage(patient.Value, ent.Comp2.TargetPart, SurgeryTendWoundsBrute) &&
                !HasTendWoundsDamage(patient.Value, ent.Comp2.TargetPart, SurgeryTendWoundsBurn),
            _ => false
        };
    }

    private int CountSurgerySteps(EntProtoId surgeryId, HashSet<EntProtoId> visited)
    {
        if (!visited.Add(surgeryId) || _surgery.GetSingleton(surgeryId) is not { } surgery ||
            !TryComp<SurgeryComponent>(surgery, out var surgeryComp))
            return 0;

        var count = surgeryComp.Steps.Count;
        if (surgeryComp.Requirement is { } requirement)
            count += CountSurgerySteps(requirement, visited);
        return count;
    }

    private bool TryFindOrganPart(Entity<AutodocComponent> ent, EntityUid organ, out EntityUid part)
    {
        part = default;
        var patient = GetPatient(ent);
        if (patient == null)
            return false;

        foreach (var (partId, _) in _body.GetBodyChildren(patient.Value))
        {
            if (_body.GetPartOrgans(partId).Any(value => value.Id == organ))
            {
                part = partId;
                return true;
            }
        }

        return false;
    }

    private EntityUid? FindAttachTarget(Entity<AutodocComponent> ent, BodyPartType type, BodyPartSymmetry symmetry)
    {
        var patient = GetPatient(ent);
        if (patient == null)
            return null;

        return type switch
        {
            BodyPartType.Head or BodyPartType.Arm or BodyPartType.Leg => FindPart(patient.Value, BodyPartType.Torso, null),
            BodyPartType.Hand => FindPart(patient.Value, BodyPartType.Arm, symmetry),
            BodyPartType.Foot => FindPart(patient.Value, BodyPartType.Leg, symmetry),
            _ => null
        };
    }

    private static EntProtoId? GetAttachSurgery(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => "SurgeryAttachHead",
            (BodyPartType.Arm, BodyPartSymmetry.Left) => "SurgeryAttachLeftArm",
            (BodyPartType.Arm, BodyPartSymmetry.Right) => "SurgeryAttachRightArm",
            (BodyPartType.Hand, BodyPartSymmetry.Left) => "SurgeryAttachLeftHand",
            (BodyPartType.Hand, BodyPartSymmetry.Right) => "SurgeryAttachRightHand",
            (BodyPartType.Leg, BodyPartSymmetry.Left) => "SurgeryAttachLeftLeg",
            (BodyPartType.Leg, BodyPartSymmetry.Right) => "SurgeryAttachRightLeg",
            (BodyPartType.Foot, BodyPartSymmetry.Left) => "SurgeryAttachLeftFoot",
            (BodyPartType.Foot, BodyPartSymmetry.Right) => "SurgeryAttachRightFoot",
            _ => null
        };
    }

    private static EntProtoId? GetOrganSurgery(string slot, bool insert)
    {
        return (slot, insert) switch
        {
            ("brain", false) => "SurgeryRemoveBrain",
            ("brain", true) => "SurgeryInsertBrain",
            ("posbrain", false) => "SurgeryRemoveBorgBrain",
            ("posbrain", true) => "SurgeryInsertBorgBrain",
            ("heart", false) => "SurgeryRemoveHeart",
            ("heart", true) => "SurgeryInsertHeart",
            ("liver", false) => "SurgeryRemoveLiver",
            ("liver", true) => "SurgeryInsertLiver",
            ("lungs", false) => "SurgeryRemoveLungs",
            ("lungs", true) => "SurgeryInsertLungs",
            ("stomach", false) => "SurgeryRemoveStomach",
            ("stomach", true) => "SurgeryInsertStomach",
            ("eyes", false) => "SurgeryRemoveEyes",
            ("eyes", true) => "SurgeryInsertEyes",
            _ => null
        };
    }

    private static string GetPartSlot(BodyPartType type, BodyPartSymmetry symmetry)
    {
        var side = symmetry switch
        {
            BodyPartSymmetry.Left => "Left",
            BodyPartSymmetry.Right => "Right",
            _ => string.Empty
        };
        return type switch
        {
            BodyPartType.Head => "Head",
            BodyPartType.Torso => "Torso",
            BodyPartType.Arm => side + "Arm",
            BodyPartType.Hand => side + "Hand",
            BodyPartType.Leg => side + "Leg",
            BodyPartType.Foot => side + "Foot",
            _ => "Other"
        };
    }

    private void UpdateUi(Entity<AutodocComponent> ent, bool forceInactive = false)
    {
        var parts = new List<AutodocBodyPartInfo>();
        var organs = new List<AutodocOrganInfo>();
        var storageItems = new List<AutodocStorageItemInfo>();
        var patient = GetPatient(ent);

        if (patient != null)
        {
            foreach (var (part, partComp) in _body.GetBodyChildren(patient.Value))
            {
                var slot = GetPartSlot(partComp.PartType, partComp.Symmetry);
                var integrity = TryComp<DamageableComponent>(part, out var damageable) && partComp.SeverIntegrity > 0
                    ? Math.Clamp(1f - damageable.TotalDamage.Float() / partComp.SeverIntegrity, 0f, 1f)
                    : 1f;
                parts.Add(new AutodocBodyPartInfo(GetNetEntity(part), Name(part), slot, integrity));
            }
        }

        if (ent.Comp.SelectedPart is { } staleSelection && !IsPatientPart(ent, staleSelection))
            ent.Comp.SelectedPart = null;

        if (ent.Comp.SelectedPart == null && parts.Count > 0)
            ent.Comp.SelectedPart = GetEntity(parts.FirstOrDefault(part => part.Slot == "Torso")?.Entity ?? parts[0].Entity);

        if (ent.Comp.SelectedPart is { } selected &&
            TryComp<BodyPartComponent>(selected, out var selectedComp))
        {
            var organBySlot = _body.GetPartOrgans(selected, selectedComp)
                .ToDictionary(value => value.Component.SlotId, value => value.Id);
            foreach (var slot in selectedComp.Organs.Keys)
            {
                var organ = organBySlot.GetValueOrDefault(slot);
                organs.Add(new AutodocOrganInfo(slot,
                    organ.Valid ? GetNetEntity(organ) : null,
                    organ.Valid ? Name(organ) : null));
            }
        }

        if (TryComp<StorageComponent>(ent, out var storage))
        {
            foreach (var item in storage.Container.ContainedEntities)
            {
                if (TryComp<BodyPartComponent>(item, out var bodyPart))
                {
                    storageItems.Add(new AutodocStorageItemInfo(
                        GetNetEntity(item), Name(item), GetPartSlot(bodyPart.PartType, bodyPart.Symmetry)));
                }
                else
                {
                    storageItems.Add(new AutodocStorageItemInfo(
                        GetNetEntity(item), Name(item), false,
                        TryComp<OrganComponent>(item, out var organ) ? organ.SlotId : null));
                }
            }
        }

        ActiveAutodocComponent? active = null;
        var busy = !forceInactive && TryComp(ent, out active);
        var progress = busy
            ? Math.Clamp((float)active!.CompletedSteps / active.TotalSteps, 0f, 1f)
            : 0f;
        var progressTarget = busy && active!.Waiting
            ? Math.Clamp((float)(active.CompletedSteps + 1) / active.TotalSteps, 0f, 1f)
            : progress;
        TimeSpan? progressStart = null;
        TimeSpan? progressEnd = null;
        if (busy && TryComp<DoAfterComponent>(ent, out var doAfters))
        {
            foreach (var doAfter in doAfters.DoAfters.Values)
            {
                if (!doAfter.Completed && !doAfter.Cancelled && doAfter.Args.Event is SurgeryDoAfterEvent)
                {
                    progressStart = doAfter.StartTime;
                    progressEnd = doAfter.StartTime + doAfter.Args.Delay;
                    break;
                }
            }
        }
        var status = busy ? Loc.GetString($"autodoc-operation-{active!.Operation.ToString().ToLowerInvariant()}") : string.Empty;
        var vitals = patient is { } patientUid ? BuildPatientVitals(patientUid, ent.Comp.SelectedPart) : null;
        _ui.SetUiState(ent.Owner, AutodocUiKey.Key, new AutodocBoundUserInterfaceState(
            patient is { } patientEntity ? GetNetEntity(patientEntity) : null,
            parts,
            ent.Comp.SelectedPart is { } selectedPart ? GetNetEntity(selectedPart) : null,
            organs,
            storageItems,
            vitals,
            busy,
            progress,
            progressTarget,
            progressStart,
            progressEnd,
            status));
    }

    private AutodocPatientVitals BuildPatientVitals(EntityUid patient, EntityUid? selectedPart)
    {
        var name = Identity.Name(patient, EntityManager);
        string? speciesId = null;
        if (TryComp<HumanoidAppearanceComponent>(patient, out var humanoid))
            speciesId = humanoid.Species;

        MobState? mobState = null;
        if (TryComp<MobStateComponent>(patient, out var mob))
            mobState = mob.CurrentState;

        var temperature = GetPatientTemperature(patient);
        var bloodLevel = float.NaN;
        var bleeding = false;
        if (TryComp<BloodstreamComponent>(patient, out var bloodstream))
        {
            bloodLevel = _bloodstream.GetBloodLevelPercentage((patient, bloodstream));
            bleeding = bloodstream.BleedAmount > 0;
        }

        var damagePerGroup = new Dictionary<string, FixedPoint2>();
        var damagePerType = new Dictionary<string, FixedPoint2>();
        var totalDamage = 0f;
        if (TryComp<DamageableComponent>(patient, out var damageable))
        {
            totalDamage = damageable.TotalDamage.Float();
            foreach (var (group, amount) in damageable.DamagePerGroup)
            {
                if (amount > FixedPoint2.Zero)
                    damagePerGroup[group] = amount;
            }

            foreach (var (type, amount) in damageable.Damage.DamageDict)
            {
                if (amount > FixedPoint2.Zero)
                    damagePerType[type] = amount;
            }
        }

        var damagedParts = new List<AutodocDamagedPartInfo>();
        foreach (var (part, partComp) in _body.GetBodyChildren(patient))
        {
            if (!TryComp<DamageableComponent>(part, out var partDamage))
                continue;

            var typed = partDamage.Damage.DamageDict
                .Where(entry => entry.Value > FixedPoint2.Zero)
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            if (typed.Count == 0)
                continue;

            damagedParts.Add(new AutodocDamagedPartInfo(
                Name(part),
                GetPartSlot(partComp.PartType, partComp.Symmetry),
                typed));
        }

        Dictionary<string, FixedPoint2>? selectedPartDamage = null;
        if (selectedPart is { } selected && TryComp<DamageableComponent>(selected, out var selectedDamage))
        {
            selectedPartDamage = selectedDamage.Damage.DamageDict
                .Where(entry => entry.Value > FixedPoint2.Zero)
                .ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        return new AutodocPatientVitals(
            name,
            speciesId,
            mobState,
            temperature,
            bloodLevel,
            bleeding,
            totalDamage,
            damagePerGroup,
            damagePerType,
            damagedParts,
            selectedPartDamage);
    }
    #endregion

    public virtual void Say(EntityUid uid, string msg)
    {
    }
}

/// <summary>
/// Error autodoc steps can use to abort the program execution and shout an error message.
/// </summary>
public sealed class AutodocError : Exception
{
    /// <summary>
    /// Message has "autodoc-error-" prepended to it, then it gets localized.
    /// </summary>
    public AutodocError(string message) : base(message)
    {
    }
}
