using System.Linq; // Lua
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components; // Lua
using Content.Shared.Hands.EntitySystems; // Lua
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Content.Shared._NF.Vehicle.Components; // Frontier
using Content.Shared.ActionBlocker; // Frontier
using Content.Shared.Actions.Components; // Frontier
using Content.Shared.Light.Components; // Frontier
using Content.Shared.Light.EntitySystems; // Frontier
using Content.Shared.Movement.Pulling.Components; // Frontier
using Content.Shared.Movement.Pulling.Events; // Frontier
using Content.Shared.Popups; // Frontier
using Robust.Shared.Network; // Frontier
using Robust.Shared.Prototypes; // Frontier
using Robust.Shared.Timing; // Frontier
using Content.Shared.Weapons.Melee.Events; // Frontier
using Content.Shared.Emag.Systems; // Frontier
using Robust.Shared.Map; // Lua
using System.Numerics; // Lua

namespace Content.Shared._Goobstation.Vehicles; // Frontier: migrate under _Goobstation

public abstract partial class SharedVehicleSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // Frontier
    [Dependency] private readonly SharedHandsSystem _hands = default!; // Lua
    [Dependency] private readonly INetManager _net = default!; // Frontier
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!; // Frontier
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!; // Frontier
    [Dependency] private readonly EmagSystem _emag = default!; // Frontier
    [Dependency] private readonly SharedPopupSystem _popup = default!; // Frontier
    [Dependency] private readonly UnpoweredFlashlightSystem _flashlight = default!; // Frontier
    [Dependency] private readonly SharedTransformSystem _transform = default!; // Lua

    public static readonly EntProtoId HornActionId = "ActionHorn";
    public static readonly EntProtoId SirenActionId = "ActionSiren";

    // Lua start
    private readonly Dictionary<EntityUid, TimeSpan> _lastNoHandsPopup = new(); // Antispam popup
    private static readonly TimeSpan NoHandsPopupCooldown = TimeSpan.FromSeconds(2); // Antispam popup
    private static int ClampRequiredHands(int requiredHands)
    { return requiredHands < 0 ? 0 : requiredHands; }
    // Lua end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VehicleComponent, MapInitEvent>(OnMapInit); // Frontier
        SubscribeLocalEvent<VehicleComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<VehicleComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<VehicleComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<VehicleComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<VehicleComponent, VirtualItemDeletedEvent>(OnDropped);
        SubscribeLocalEvent<VehicleComponent, MeleeHitEvent>(OnMeleeHit); // Frontier
        SubscribeLocalEvent<VehicleComponent, GotEmaggedEvent>(OnGotEmagged, before: [typeof(UnpoweredFlashlightSystem)]); // Frontier
        SubscribeLocalEvent<VehicleComponent, GotUnEmaggedEvent>(OnGotUnemagged, before: [typeof(UnpoweredFlashlightSystem)]); // Frontier

        SubscribeLocalEvent<VehicleComponent, EntInsertedIntoContainerMessage>(OnInsert);
        SubscribeLocalEvent<VehicleComponent, EntRemovedFromContainerMessage>(OnEject);

        SubscribeLocalEvent<VehicleComponent, HornActionEvent>(OnHorn);
        SubscribeLocalEvent<VehicleComponent, SirenActionEvent>(OnSiren);

        SubscribeLocalEvent<VehicleRiderComponent, PullAttemptEvent>(OnRiderPull); // Frontier
    }

    private void OnInit(EntityUid uid, VehicleComponent component, ComponentInit args)
    {
        _appearance.SetData(uid, VehicleState.Animated, component.EngineRunning && component.Driver != null); // Frontier: add Driver != null
        _appearance.SetData(uid, VehicleState.DrawOver, false);
    }

    // Frontier
    private void OnMapInit(EntityUid uid, VehicleComponent component, MapInitEvent args)
    {
        bool actionsUpdated = false;
        if (component.HornSound != null)
        {
            _actionContainer.EnsureAction(uid, ref component.HornAction, HornActionId);
            actionsUpdated = true;
        }

        if (component.SirenSound != null)
        {
            _actionContainer.EnsureAction(uid, ref component.SirenAction, SirenActionId);
            actionsUpdated = true;
        }

        if (actionsUpdated)
            Dirty(uid, component);
    }
    // End Frontier

    private void OnRemove(EntityUid uid, VehicleComponent component, ComponentRemove args)
    {
        if (component.Driver == null)
            return;

        _buckle.TryUnbuckle(component.Driver.Value, component.Driver.Value);
        Dismount(component.Driver.Value, uid);
        _appearance.SetData(uid, VehicleState.DrawOver, false);
    }

    private void OnInsert(EntityUid uid, VehicleComponent component, ref EntInsertedIntoContainerMessage args)
    {
        if (HasComp<InstantActionComponent>(args.Entity))
            return;

        // Frontier: check key slot
        if (args.Container.ID != component.KeySlotId)
            return;
        if (!_timing.IsFirstTimePredicted)
            return;
        // End Frontier: check key slot

        component.EngineRunning = true;
        _appearance.SetData(uid, VehicleState.Animated, component.Driver != null);

        _ambientSound.SetAmbience(uid, true);

        if (component.Driver == null)
            return;

        Mount(component.Driver.Value, uid);
    }

    private void OnEject(EntityUid uid, VehicleComponent component, ref EntRemovedFromContainerMessage args)
    {
        // Frontier: check key slot
        if (args.Container.ID != component.KeySlotId)
            return;
        if (!_timing.IsFirstTimePredicted)
            return;
        // End Frontier: check key slot

        component.EngineRunning = false;
        _appearance.SetData(uid, VehicleState.Animated, false);

        _ambientSound.SetAmbience(uid, false);

        if (component.Driver == null)
            return;

        Dismount(component.Driver.Value, uid, removeDriver: false); // Frontier: add removeDriver: false - the driver is still around.
    }

    private void OnHorn(EntityUid uid, VehicleComponent component, InstantActionEvent args)
    {
        if (args.Handled == true || component.Driver != args.Performer || component.HornSound == null)
            return;

        _audio.PlayPredicted(component.HornSound, uid, args.Performer); // Frontier: PlayPvs<PlayPredicted, add args.Performer
        args.Handled = true;
    }

    private void OnSiren(EntityUid uid, VehicleComponent component, InstantActionEvent args)
    {
        if (_net.IsClient) // Frontier: _audio.Stop hates client-side entities, only create this serverside
            return; // Frontier

        if (args.Handled == true || component.Driver != args.Performer || component.SirenSound == null)
            return;

        if (component.SirenStream != null) // Frontier: SirenEnabled<SirenStream != null
        {
            component.SirenStream = _audio.Stop(component.SirenStream);
        }
        else
        {
            var sirenParams = component.SirenSound.Params.WithLoop(true); // Frontier: force loop
            component.SirenStream = _audio.PlayPvs(component.SirenSound, uid, audioParams: sirenParams)?.Entity; // Frontier: set params
        }

        // component.SirenEnabled = component.SirenStream != null; // Frontier: remove (unneeded state)
        args.Handled = true;
    }

    // Lua start (hands occupancy)
    private bool TryOccupyHands(EntityUid rider, EntityUid vehicle, int requiredHands)
    {
        requiredHands = ClampRequiredHands(requiredHands);
        if (requiredHands == 0) return true;
        if (!TryComp<HandsComponent>(rider, out var hands))
            return false;
        var alreadyOccupied = 0;
        var emptyHands = new List<string>();
        foreach (var handId in hands.Hands.Keys)
        {
            if (_hands.HandIsEmpty((rider, hands), handId))
            { emptyHands.Add(handId); continue; }
            if (_hands.TryGetHeldItem((rider, hands), handId, out var heldEntity) && TryComp<VirtualItemComponent>(heldEntity.Value, out var virt) && virt.BlockingEntity == vehicle)
            { alreadyOccupied++; }
        }
        var needed = requiredHands - alreadyOccupied;
        if (needed <= 0) return true;
        if (emptyHands.Count < needed) return false;
        foreach (var handId in emptyHands.Take(needed))
        { if (!_virtualItem.TrySpawnVirtualItemInHand(vehicle, rider, out _, dropOthers: false, empty: handId)) return false; }
        return true;
    }

    private bool EnsureHandsAreCorrect(EntityUid rider, EntityUid vehicle, int requiredHands)
    {
        requiredHands = ClampRequiredHands(requiredHands);
        if (!TryComp<HandsComponent>(rider, out var hands)) return requiredHands == 0;
        var matchingVirtualItems = new List<EntityUid>();
        var emptyHands = new List<string>();
        foreach (var handId in hands.Hands.Keys)
        {
            if (_hands.HandIsEmpty((rider, hands), handId))
            { emptyHands.Add(handId); continue; }
            if (_hands.TryGetHeldItem((rider, hands), handId, out var heldEntity) && TryComp<VirtualItemComponent>(heldEntity.Value, out var virt) && virt.BlockingEntity == vehicle)
            { matchingVirtualItems.Add(heldEntity.Value); }
        }
        if (matchingVirtualItems.Count > requiredHands)
        {
            foreach (var extra in matchingVirtualItems.Skip(requiredHands))
            { if (TryComp<VirtualItemComponent>(extra, out var virt)) _virtualItem.DeleteVirtualItem((extra, virt), rider); }
        }
        var current = Math.Min(matchingVirtualItems.Count, requiredHands);
        var needed = requiredHands - current;
        if (needed > 0)
        {
            if (emptyHands.Count < needed) return false;
            foreach (var handId in emptyHands.Take(needed))
            { if (!_virtualItem.TrySpawnVirtualItemInHand(vehicle, rider, out _, dropOthers: false, empty: handId)) return false; }
        }
        if (requiredHands == 0 && matchingVirtualItems.Count > 0) _virtualItem.DeleteInHandsMatching(rider, vehicle);
        return true;
    }
    // Lua start
    private bool ShouldShowNoHandsPopup(EntityUid user)
    {
        if (!_net.IsClient) return true;
        var now = _timing.CurTime;
        if (_lastNoHandsPopup.TryGetValue(user, out var last) && now - last < NoHandsPopupCooldown) return false;
        _lastNoHandsPopup[user] = now;
        return true;
    }
    // Lua end

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var comp in EntityQuery<VehicleRiderComponent>())
        {
            var rider = comp.Owner;
            if (Transform(rider).ParentUid is { } vehicle &&
                HasComp<VehicleComponent>(vehicle))
            {
                var vehicleComp = Comp<VehicleComponent>(vehicle);
                if (vehicleComp.Driver == rider && !EnsureHandsAreCorrect(rider, vehicle, vehicleComp.RequiredHands))
                {
                    _buckle.TryUnbuckle(rider, vehicle);
                    if (ShouldShowNoHandsPopup(rider)) _popup.PopupPredicted(Loc.GetString("vehicle-no-free-hands"), vehicle, rider);
                }
            }

            // Lua start
            if (TryComp(rider, out BuckleComponent? buckle) && buckle.BuckledTo is { } strapEnt)
            {
                var strapUid = strapEnt;
                if (TryComp<VehicleComponent>(strapUid, out var vehicleComp))
                {
                    var riderXform = Transform(rider);
                    if (riderXform.ParentUid != strapUid)
                    {
                        Vector2 offset = Vector2.Zero;
                        if (vehicleComp.Passenger == rider)
                        { offset = GetPassengerOffset(new Entity<VehicleComponent>(strapUid, vehicleComp)); }
                        var coords = new EntityCoordinates(strapUid, offset);
                        _transform.SetCoordinates(rider, riderXform, coords, rotation: null);
                    }
                    else if (vehicleComp.Passenger == rider)
                    {
                        var passengerOffset = GetPassengerOffset(new Entity<VehicleComponent>(strapUid, vehicleComp));
                        var currentOffset = riderXform.LocalPosition;
                        if ((currentOffset - passengerOffset).LengthSquared() > 0.01f)
                        {
                            var coords = new EntityCoordinates(strapUid, passengerOffset);
                            _transform.SetCoordinates(rider, riderXform, coords, rotation: null);
                        }
                        if (TryComp<StrapComponent>(strapUid, out var strapComp))
                        {
                            strapComp.BuckleOffset = passengerOffset;
                            Dirty(strapUid, strapComp);
                        }
                    }
                }
            } // Lua end
        }
    }
    // Lua end (fuck driver cowboy)

    private void OnStrapAttempt(Entity<VehicleComponent> ent, ref StrapAttemptEvent args)
    {
        var rider = args.Buckle.Owner; // i dont want to re write this shit 100 fucking times
        bool isDriver = ent.Comp.Driver == null;
        bool isPassenger = !isDriver && ent.Comp.Passenger == null;

        if (!isDriver && !isPassenger)
        {
            args.Cancelled = true;
            return;
        }

        // Frontier: no pulling when riding
        if (TryComp<PullerComponent>(args.Buckle, out var puller) && puller.Pulling != null)
        {
            _popup.PopupPredicted(Loc.GetString("vehicle-cannot-pull", ("object", puller.Pulling), ("vehicle", ent)), ent, args.Buckle);
            args.Cancelled = true;
            return;
        }
        // End Frontier

        // Lua start
        if (isDriver)
        {
            //if (ent.Comp.RequiredHands != 2)
            //{
            //    for (int hands = 2; hands < ent.Comp.RequiredHands; hands++)
            //    {
            //        if (!_virtualItem.TrySpawnVirtualItemInHand(ent.Owner, driver, false))
            //        {
            //            args.Cancelled = true;
            //            _virtualItem.DeleteInHandsMatching(driver, ent.Owner);
            //            return;
            //        }
            //    }

            var requiredHands = ClampRequiredHands(ent.Comp.RequiredHands);
            if (requiredHands > 0)
            {
                if (!TryComp<HandsComponent>(rider, out var hands))
                {
                    args.Cancelled = true;
                    return;
                }

                var emptyHands = hands.Hands.Keys.Count(handId => _hands.HandIsEmpty((rider, hands), handId));
                if (emptyHands < requiredHands)
                {
                    if (ShouldShowNoHandsPopup(rider)) _popup.PopupPredicted(Loc.GetString("vehicle-no-free-hands"), ent, rider);
                    args.Cancelled = true;
                    return;
                }
            }
        }
        // Lua end

        // AddHorns(driver, ent); // Frontier: delay until mounted
    }

    private Vector2 GetPassengerOffset(Entity<VehicleComponent> vehicle)
    {
        var angle = _transform.GetWorldRotation(vehicle);
        if (angle < 0) angle += 2 * MathF.PI;
        Vector2 offset;
        if (angle >= 7 * MathF.PI / 4 || angle < MathF.PI / 4) offset = vehicle.Comp.PassengerEastOffset;
        else if (angle >= MathF.PI / 4 && angle < 3 * MathF.PI / 4) offset = vehicle.Comp.PassengerNorthOffset;
        else if (angle >= 3 * MathF.PI / 4 && angle < 5 * MathF.PI / 4) offset = vehicle.Comp.PassengerWestOffset;
        else offset = vehicle.Comp.PassengerSouthOffset;
        return offset;
    }

    protected virtual void OnStrapped(Entity<VehicleComponent> ent, ref StrappedEvent args) //Lua: private void<protected virtual void
    {
        var rider = args.Buckle.Owner;
        bool isDriver = ent.Comp.Driver == null;
        bool isPassenger = !isDriver && ent.Comp.Passenger == null;

        if (isDriver)
        {
            // Lua start (fuck driver cowboy)
            if (!TryOccupyHands(rider, ent.Owner, ent.Comp.RequiredHands))
            {
                _buckle.TryUnbuckle(rider, ent.Owner);
                if (ShouldShowNoHandsPopup(rider)) _popup.PopupPredicted(Loc.GetString("vehicle-no-free-hands"), ent, rider);
                return;
            }
            // Lua end  (fuck driver cowboy)
            if (!TryComp(rider, out MobMoverComponent? mover))
                return;

            ent.Comp.Driver = rider;
            Dirty(ent); // Frontier
            _appearance.SetData(ent.Owner, VehicleState.DrawOver, true);
            _appearance.SetData(ent.Owner, VehicleState.Animated, ent.Comp.EngineRunning); // Frontier
            var riderComp = EnsureComp<VehicleRiderComponent>(rider); // Frontier
            Dirty(rider, riderComp); // Frontier

            if (!ent.Comp.EngineRunning)
                return;

            Mount(rider, ent.Owner);
        }
        else if (isPassenger)
        {
            ent.Comp.Passenger = rider;
            Dirty(ent);
            var riderComp = EnsureComp<VehicleRiderComponent>(rider);
            Dirty(rider, riderComp);
            var passengerOffset = GetPassengerOffset(ent);
            if (TryComp<StrapComponent>(ent.Owner, out var strapComp))
            {
                strapComp.BuckleOffset = passengerOffset;
                Dirty(ent.Owner, strapComp);
            }

            var riderXform = Transform(rider);
            var coords = new EntityCoordinates(ent.Owner, passengerOffset);
            _transform.SetCoordinates(rider, riderXform, coords, rotation: null);
        }
    }

    protected virtual void OnUnstrapped(Entity<VehicleComponent> ent, ref UnstrappedEvent args) //Lua: private void<protected virtual void
    {
        var rider = args.Buckle.Owner;

        if (ent.Comp.Driver == rider)
        {
            Dismount(rider, ent);
            _appearance.SetData(ent.Owner, VehicleState.DrawOver, false);
            _appearance.SetData(ent.Owner, VehicleState.Animated, false); // Frontier
            RemComp<VehicleRiderComponent>(rider); // Frontier
        }
        else if (ent.Comp.Passenger == rider)
        {
            ent.Comp.Passenger = null;
            Dirty(ent); // Frontier
            RemComp<VehicleRiderComponent>(rider); // Frontier
        }
    }

    private void OnDropped(EntityUid uid, VehicleComponent comp, VirtualItemDeletedEvent args)
    {
        if (comp.Driver == args.User)
        {
            _buckle.TryUnbuckle(args.User, args.User);

            Dismount(args.User, uid);
            _appearance.SetData(uid, VehicleState.DrawOver, false);
            _appearance.SetData(uid, VehicleState.Animated, false); // Frontier
            RemComp<VehicleRiderComponent>(args.User); // Frontier
        }
        else if (comp.Passenger == args.User)
        {
            _buckle.TryUnbuckle(args.User, args.User);
            comp.Passenger = null;
            Dirty(uid, comp);
            RemComp<VehicleRiderComponent>(args.User);
        }
    }

    // Frontier: do not hit your own vehicle
    private void OnMeleeHit(Entity<VehicleComponent> ent, ref MeleeHitEvent args)
    {
        if (args.User == ent.Comp.Driver) // Don't hit your own vehicle
            args.Handled = true;
    }
    // End Frontier: do not hit your own vehicle

    private void AddHorns(EntityUid driver, EntityUid vehicle)
    {
        if (!TryComp<VehicleComponent>(vehicle, out var vehicleComp))
            return;

        // Frontier: grant existing actions
        List<EntityUid> grantedActions = new();
        if (vehicleComp.HornAction != null)
            grantedActions.Add(vehicleComp.HornAction.Value);

        if (vehicleComp.SirenAction != null)
            grantedActions.Add(vehicleComp.SirenAction.Value);

        if (TryComp<UnpoweredFlashlightComponent>(vehicle, out var flashlight) && flashlight.ToggleActionEntity != null)
        {
            grantedActions.Add(flashlight.ToggleActionEntity.Value);
            _flashlight.SetLight((vehicle, flashlight), flashlight.LightOn, quiet: true);
        }
        // Only try to grant actions if the vehicle actually has them.
        if (grantedActions.Count > 0)
            _actions.GrantActions(driver, grantedActions, vehicle);
        // End Frontier
    }

    private void Mount(EntityUid driver, EntityUid vehicle)
    {
        if (TryComp<AccessComponent>(vehicle, out var accessComp))
        {
            var accessSources = _access.FindPotentialAccessItems(driver);
            var access = _access.FindAccessTags(driver, accessSources);

            foreach (var tag in access)
            {
                accessComp.Tags.Add(tag);
            }
        }

        _mover.SetRelay(driver, vehicle);

        AddHorns(driver, vehicle); // Frontier
    }

    private void Dismount(EntityUid driver, EntityUid vehicle, bool removeDriver = true) // Frontier: add removeDriver
    {
        if (!TryComp<VehicleComponent>(vehicle, out var vehicleComp) || vehicleComp.Driver != driver)
            return;

        RemComp<RelayInputMoverComponent>(driver);
        _actionBlocker.UpdateCanMove(driver); // Frontier: bugfix, relay input mover only updates on shutdown, not remove

        if (removeDriver) // Frontier
            vehicleComp.Driver = null;

        _actions.RemoveProvidedActions(driver, vehicle); // Frontier: don't remove actions, just provide/revoke them

        if (removeDriver) // Frontier
            _virtualItem.DeleteInHandsMatching(driver, vehicle);

        if (TryComp<AccessComponent>(vehicle, out var accessComp))
            accessComp.Tags.Clear();
    }

    // Frontier: prevent drivers from pulling things, emag handlers
    private void OnRiderPull(Entity<VehicleRiderComponent> ent, ref PullAttemptEvent args)
    {
        if (args.PullerUid == ent.Owner)
            args.Cancelled = true;
    }

    private void OnGotEmagged(Entity<VehicleComponent> ent, ref GotEmaggedEvent args)
    {
        if (args.Handled)
            return;

        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (ent.Comp.RadarBlip)
        {
            ent.Comp.RadarBlip = false;
            Dirty(ent);

            HandleEmag(ent);

            // Hack: assuming the only other emaggable component on the vehicle is a flashlight
            args.Repeatable = HasComp<UnpoweredFlashlightComponent>(ent);
            args.Handled = true;
        }
    }

    private void OnGotUnemagged(Entity<VehicleComponent> ent, ref GotUnEmaggedEvent args)
    {
        if (args.Handled)
            return;

        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (!ent.Comp.RadarBlip)
        {
            ent.Comp.RadarBlip = true;
            Dirty(ent);

            HandleUnemag(ent);

            args.Handled = true;
        }
    }

    protected abstract void HandleEmag(Entity<VehicleComponent> ent);
    protected abstract void HandleUnemag(Entity<VehicleComponent> ent);
    // End Frontier
}

public sealed partial class HornActionEvent : InstantActionEvent;

public sealed partial class SirenActionEvent : InstantActionEvent;
