using Content.Shared._Shitmed.Autodoc;
using Content.Shared._Shitmed.Autodoc.Components;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Labels.EntitySystems;
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
            s.Event<AutodocCreateProgramMessage>(OnCreateProgram);
            s.Event<AutodocToggleProgramSafetyMessage>(OnToggleProgramSafety);
            s.Event<AutodocRemoveProgramMessage>(OnRemoveProgram);
            s.Event<AutodocAddStepMessage>(OnAddStep);
            s.Event<AutodocRemoveStepMessage>(OnRemoveStep);
            s.Event<AutodocStartMessage>(OnStart);
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
    }

    private void OnBodyRemoved(Entity<AutodocComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.BodyContainerId)
            return;

        // Ejecting the patient aborts any running program.
        if (IsActive(ent))
            RemCompDeferred<ActiveAutodocComponent>(ent);

        UpdateAppearance(ent);
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

    private void OnCreateProgram(Entity<AutodocComponent> ent, ref AutodocCreateProgramMessage args)
    {
        CreateProgram(ent, args.Title);
    }

    private void OnToggleProgramSafety(Entity<AutodocComponent> ent, ref AutodocToggleProgramSafetyMessage args)
    {
        if (IsActive(ent))
            return;

        if (args.Program >= ent.Comp.Programs.Count)
            return;

        var program = ent.Comp.Programs[args.Program];
        program.SkipFailed ^= true;
        Dirty(ent);

        _adminLogger.Add(LogType.InteractActivate, LogImpact.Low, $"{ToPrettyString(args.Actor):user} toggled safety of autodoc program {program.Title}");
    }

    private void OnRemoveProgram(Entity<AutodocComponent> ent, ref AutodocRemoveProgramMessage args)
    {
        RemoveProgram(ent, args.Program);
    }

    private void OnAddStep(Entity<AutodocComponent> ent, ref AutodocAddStepMessage args)
    {
        if (!args.Step.Validate(ent, this))
        {
            Log.Warning($"User {ToPrettyString(args.Actor)} tried to add an invalid autodoc step!");
            return;
        }

        AddStep(ent, args.Program, args.Step, args.Index, args.Actor);
    }

    private void OnRemoveStep(Entity<AutodocComponent> ent, ref AutodocRemoveStepMessage args)
    {
        RemoveStep(ent, args.Program, args.Step);
    }

    private void OnStart(Entity<AutodocComponent> ent, ref AutodocStartMessage args)
    {
        StartProgram(ent, args.Program, args.Actor);
    }

    private void OnStop(Entity<AutodocComponent> ent, ref AutodocStopMessage args)
    {
        RemComp<ActiveAutodocComponent>(ent);
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
            ent.Comp.Waiting = false; // try the next autodoc or surgery step
            return;
        }

        // for tend wounds dont abort, more wounds need tending
        if (HasComp<SurgeryRepeatableStepComponent>(args.Step))
            return;

        ent.Comp.Waiting = repeatable;
    }

    private void OnSurgeryStepFailed(Entity<ActiveAutodocComponent> ent, ref SurgeryStepFailedEvent args)
    {
        if (!TryComp<AutodocComponent>(ent, out var comp))
            return;

        var program = comp.Programs[ent.Comp.CurrentProgram];
        var error = Loc.GetString("autodoc-error-surgery-failed");
        if (program.SkipFailed)
        {
            Say(ent, Loc.GetString("autodoc-error", ("error", error)));
            ent.Comp.ProgramStep++;
        }
        else
        {
            Say(ent, Loc.GetString("autodoc-fatal-error", ("error", error)));
            RemCompDeferred<ActiveAutodocComponent>(ent);
        }
    }

    private void OnActiveShutdown(Entity<ActiveAutodocComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<AutodocComponent>(ent, out var comp))
            return;

        // wake the patient when program completes or errors out
        if (GetPatient((ent.Owner, comp)) is {} patient)
            WakePatient(patient);
    }

    protected virtual void WakePatient(EntityUid patient)
    {
        _sleeping.TryWaking(patient);
    }

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
        if (_hands.GetHeldItem((ent.Owner, ent.Comp2), ent.Comp1.ItemSlot) is not {} item)
            return; // Body parts are inserted directly during capture.

        if (!_storage.Insert(ent, item, out _))
            throw new AutodocError("storage-full");
    }

    public EntityUid GetHeldOrThrow(Entity<AutodocComponent, HandsComponent> ent)
    {
        if (TryGetOperatedItem(ent) is not {} item)
            throw new AutodocError("item-unavailable");

        return item;
    }

    /// <summary>
    /// Returns the item currently held for surgery, or a body part already placed in storage during capture.
    /// </summary>
    public EntityUid? TryGetOperatedItem(Entity<AutodocComponent, HandsComponent> ent)
    {
        if (_hands.GetHeldItem((ent.Owner, ent.Comp2), ent.Comp1.ItemSlot) is {} held)
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
        if (GetPatient(ent) is not {} patient)
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
        if (_surgery.GetSingleton(surgery) is not {} singleton)
            throw new AutodocError("reality-breaking");

        if (_surgery.GetNextStep(patient, part, singleton) is not {} pair)
            return false;

        var nextSurgery = pair.Item1;
        if (MetaData(nextSurgery).EntityPrototype?.ID is not {} surgeryId) // should never happen
            throw new AutodocError("reality-breaking");

        var index = pair.Item2;
        var nextStep = nextSurgery.Comp.Steps[index];
        if (!_surgery.TryDoSurgeryStep(patient, part, ent, surgeryId, nextStep, out var error))
        {
            // if the omnitool is held inserting organ etc will fail
            // may need to swap hands to the selected item instead of omnitool
            // if that works then it'll swap back automatically for the next step
            if (error != StepInvalidReason.MissingTool && error != StepInvalidReason.ToolInvalid)
                throw new AutodocError($"step-invalid-{error}");

            TrySwapAutodocHand(ent);
            if (!_surgery.TryDoSurgeryStep(patient, part, ent, surgeryId, nextStep, out error))
                throw new AutodocError($"step-invalid-{error}"); // no trying again just fail
        }

        var comp = Comp<ActiveAutodocComponent>(ent);
        comp.CurrentSurgery = (patient, part, surgery);
        comp.Waiting = true; // don't go onto next step until doafter finishes
        return true;
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

    /// <summary>
    /// Create a blank program and return the index to it.
    /// Programs cannot be created while operating or if there are too many, in which case it will return null.
    /// </summary>
    public int? CreateProgram(Entity<AutodocComponent> ent, string title)
    {
        var index = ent.Comp.Programs.Count;
        if (IsActive(ent) || index >= ent.Comp.MaxPrograms)
            return null;

        if (string.IsNullOrEmpty(title) || title.Length > ent.Comp.MaxProgramTitleLength)
            return null;

        ent.Comp.Programs.Add(new AutodocProgram()
        {
            Title = title
        });
        Dirty(ent);
        return index;
    }

    /// <summary>
    /// Removes a program at an index, returning true if it succeeded.
    /// </summary>
    public bool RemoveProgram(Entity<AutodocComponent> ent, int index)
    {
        if (IsActive(ent) || index >= ent.Comp.Programs.Count)
            return false;

        ent.Comp.Programs.RemoveAt(index);
        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Adds a step to a program at an index, returning true if it succeeded.
    /// </summary>
    public bool AddStep(Entity<AutodocComponent> ent, int programIndex, IAutodocStep step, int index, EntityUid user)
    {
        if (IsActive(ent) || programIndex >= ent.Comp.Programs.Count)
            return false;

        var program = ent.Comp.Programs[programIndex];
        if (program.Steps.Count >= ent.Comp.MaxProgramSteps || index < 0 || index > program.Steps.Count)
            return false;

        program.Steps.Insert(index, step);
        Dirty(ent);

        _adminLogger.Add(LogType.InteractActivate, LogImpact.Low, $"{ToPrettyString(user):user} added step '{step.Title}' to autodoc program '{program.Title}'");
        return true;
    }

    /// <summary>
    /// Removes a step from a program, returning true if it succeeded.
    /// </summary>
    public bool RemoveStep(Entity<AutodocComponent> ent, int programIndex, int step)
    {
        if (IsActive(ent) || programIndex >= ent.Comp.Programs.Count)
            return false;

        var program = ent.Comp.Programs[programIndex];
        if (step >= program.Steps.Count)
            return false;

        program.Steps.RemoveAt(step);
        Dirty(ent);
        return true;
    }

    public bool IsActive(EntityUid uid)
    {
        return HasComp<ActiveAutodocComponent>(uid);
    }

    public AutodocProgram CurrentProgram(Entity<AutodocComponent, ActiveAutodocComponent> ent)
    {
        // not checking if it exists since Programs isnt allowed to be changed while operating
        return ent.Comp1.Programs[ent.Comp2.CurrentProgram];
    }

    public bool StartProgram(Entity<AutodocComponent> ent, int index, EntityUid user)
    {
        // no error since UI checks this too
        if (IsActive(ent) || index >= ent.Comp.Programs.Count || GetPatient(ent) is not {} patient)
            return false;

        var active = EnsureComp<ActiveAutodocComponent>(ent);
        active.CurrentProgram = index;
        active.NextUpdate = Timing.CurTime + ent.Comp.UpdateDelay;
        Dirty(ent.Owner, active);

        _adminLogger.Add(LogType.InteractActivate, LogImpact.High, $"{ToPrettyString(user):user} started autodoc program '{ent.Comp.Programs[index].Title}' on {ToPrettyString(patient):patient}");
        return true;
    }

    /// <summary>
    /// Tries to start the next step, shouting the error if it fails.
    /// Returns true if the program is being stopped.
    /// </summary>
    public bool Proceed(Entity<AutodocComponent, ActiveAutodocComponent> ent)
    {
        if (ent.Comp2.Waiting)
            return false;

        try
        {
            // stay on this AutodocSurgeryStep until every step of the surgery (and its dependencies) is complete
            // if this was the last step, StartSurgery will fail and the next autodoc step will run
            if (ent.Comp2.CurrentSurgery is {} args)
            {
                var (body, part, surgery) = args;
                if (StartSurgeryOrThrow((ent.Owner, ent.Comp1), body, part, surgery))
                    return false;

                // done with the surgery onto next step!!!
                ent.Comp2.CurrentSurgery = null;
                ent.Comp2.ProgramStep++;
            }

            var program = ent.Comp1.Programs[ent.Comp2.CurrentProgram];
            var index = ent.Comp2.ProgramStep;
            if (index >= program.Steps.Count)
            {
                Say(ent, Loc.GetString("autodoc-program-completed"));
                return true;
            }
            var step = program.Steps[index];
            if (step.Run((ent.Owner, ent.Comp1, Comp<HandsComponent>(ent)), this))
                ent.Comp2.ProgramStep++;
            else
                ent.Comp2.Waiting = true;
        }
        catch (AutodocError e)
        {
            var error = Loc.GetString("autodoc-error-" + e.Message);
            var program = ent.Comp1.Programs[ent.Comp2.CurrentProgram];
            var skipNoDamageTendWounds = ShouldSkipNoDamageTendWounds(ent, e.Message);
            if (program.SkipFailed || skipNoDamageTendWounds)
            {
                Say(ent, skipNoDamageTendWounds
                    ? Loc.GetString("autodoc-error-tend-wounds-no-damage")
                    : Loc.GetString("autodoc-error", ("error", error)));
                ent.Comp2.ProgramStep++;
            }
            else
            {
                Say(ent, Loc.GetString("autodoc-fatal-error", ("error", error)));
                return true;
            }
        }

        Dirty(ent.Owner, ent.Comp1);
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

    private bool ShouldSkipNoDamageTendWounds(Entity<AutodocComponent, ActiveAutodocComponent> ent, string autodocErrorMessage)
    {
        // Only handle the "patient unfit for surgery" style error; we treat it as "nothing to tend" instead.
        if (autodocErrorMessage != "step-invalid-SurgeryInvalid")
            return false;

        if (ent.Comp2.ProgramStep < 0
            || ent.Comp2.CurrentProgram < 0
            || ent.Comp2.CurrentProgram >= ent.Comp1.Programs.Count)
            return false;

        var program = ent.Comp1.Programs[ent.Comp2.CurrentProgram];
        if (ent.Comp2.ProgramStep >= program.Steps.Count)
            return false;

        if (program.Steps[ent.Comp2.ProgramStep] is not SurgeryAutodocStep surgeryStep
            || !IsTendWoundsSurgery(surgeryStep.Surgery))
            return false;

        var patient = GetPatient((ent.Owner, ent.Comp1));
        if (patient is not {} patientUid)
            return true;

        if (FindPart(patientUid, surgeryStep.Part, surgeryStep.Symmetry) is not {} part)
            return true;

        return !HasTendWoundsDamage(patientUid, part, surgeryStep.Surgery);
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
