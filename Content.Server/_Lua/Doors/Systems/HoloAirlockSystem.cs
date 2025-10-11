// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeviceLinking.Components;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Wires;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.Doors;
using Content.Shared._Lua.Doors.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Wires;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server._Lua.Doors.Systems;

public sealed class HoloAirlockSystem : EntitySystem
{
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly AirtightSystem _airtight = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly WiresSystem _wiresSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HoloAirlockComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HoloAirlockComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<HoloAirlockComponent, DoorBoltsChangedEvent>(OnBoltsChanged);
        SubscribeLocalEvent<HoloAirlockComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<HoloAirlockComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnStartup(Entity<HoloAirlockComponent> ent, ref ComponentStartup args)
    { UpdateState(ent.Owner); }

    private void OnSignalReceived(EntityUid uid, HoloAirlockComponent comp, ref SignalReceivedEvent args)
    {
        if (!TryComp<DoorSignalControlComponent>(uid, out var ctrl) || !TryComp<DoorBoltComponent>(uid, out var bolts)) return;
        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);
        if (args.Port == ctrl.InBolt)
        {
            bool bolt;
            if (state == SignalState.Momentary) bolt = !bolts.BoltsDown;
            else bolt = state == SignalState.High;
            _door.SetBoltsDown((uid, bolts), bolt);
        }
    }

    private void OnPowerChanged(Entity<HoloAirlockComponent> ent, ref PowerChangedEvent args)
    { UpdateState(ent.Owner); }

    private void OnBoltsChanged(Entity<HoloAirlockComponent> ent, ref DoorBoltsChangedEvent args)
    { UpdateState(ent.Owner); }

    private void OnActivate(EntityUid uid, HoloAirlockComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex) return;
        if (TryComp<WiresPanelComponent>(uid, out var panel) && panel.Open && TryComp<ActorComponent>(args.User, out var actor))
        {
            if (TryComp<WiresPanelSecurityComponent>(uid, out var sec) && !sec.WiresAccessible) return;
            _wiresSystem.OpenUserInterface(uid, actor.PlayerSession);
            args.Handled = true;
        }
    }

    private void UpdateState(EntityUid uid)
    {
        var powered = TryComp(uid, out ApcPowerReceiverComponent? recv) && recv.Powered;
        var bolted = TryComp<DoorBoltComponent>(uid, out var boltComp) && _door.IsBolted(uid, boltComp);
        var docked = TryComp(uid, out DockingComponent? docking) && docking.Docked;
        var collidable = powered && bolted;
        var airblocked = powered && !docked;
        var passable = powered && !bolted;
        if (TryComp(uid, out PhysicsComponent? physics)) _physics.SetCanCollide(uid, collidable, body: physics);
        if (TryComp(uid, out AirtightComponent? airtight)) _airtight.SetAirblocked((uid, airtight), airblocked);
        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.SetData(uid, DoorVisuals.Powered, powered, appearance);
            _appearance.SetData(uid, DoorVisuals.ClosedLights, passable, appearance);
        }
    }
}


