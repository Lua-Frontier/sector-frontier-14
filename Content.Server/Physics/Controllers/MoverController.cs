using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Server.Physics.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server._Lua.Shuttles.Systems; // Lua
using Content.Server._Lua.SpaceHazards;
using Content.Shared.Friction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Ghost; // Frontier
using Prometheus;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using DroneConsoleComponent = Content.Server.Shuttles.DroneConsoleComponent;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;
using Robust.Shared.Map.Components;

namespace Content.Server.Physics.Controllers;

public sealed class MoverController : SharedMoverController
{
    private static readonly Gauge ActiveMoverGauge = Metrics.CreateGauge(
        "physics_active_mover_count",
        "Active amount of InputMovers being processed by MoverController");

    [Dependency] private readonly ThrusterSystem _thruster = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly ShuttleTabletSystem _tablet = default!; // Lua
    [Dependency] private readonly NebulaEnvironmentSystem _nebulaEnvironment = default!;

    private EntityQuery<ShuttleComponent> _shuttleQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private Dictionary<EntityUid, (ShuttleComponent, List<(EntityUid, PilotComponent, InputMoverComponent, TransformComponent)>)> _shuttlePilots = new();

    public override void Initialize()
    {
        base.Initialize();
        _shuttleQuery = GetEntityQuery<ShuttleComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<RelayInputMoverComponent, PlayerAttachedEvent>(OnRelayPlayerAttached);
        SubscribeLocalEvent<RelayInputMoverComponent, PlayerDetachedEvent>(OnRelayPlayerDetached);
        SubscribeLocalEvent<InputMoverComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<InputMoverComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PilotComponent, GetShuttleInputsEvent>(OnPilotGetInputs); // Mono

        // Mono: shuttle AI / non-player input sources
        SubscribeLocalEvent<PilotedShuttleComponent, StartCollideEvent>(PilotedShuttleRelayEvent<StartCollideEvent>);
    }

    private void OnRelayPlayerAttached(Entity<RelayInputMoverComponent> entity, ref PlayerAttachedEvent args)
    {
        if (MoverQuery.TryGetComponent(entity.Comp.RelayEntity, out var inputMover))
            SetMoveInput((entity.Comp.RelayEntity, inputMover), MoveButtons.None);
    }

    private void OnRelayPlayerDetached(Entity<RelayInputMoverComponent> entity, ref PlayerDetachedEvent args)
    {
        if (MoverQuery.TryGetComponent(entity.Comp.RelayEntity, out var inputMover))
            SetMoveInput((entity.Comp.RelayEntity, inputMover), MoveButtons.None);
    }

    private void OnPlayerAttached(Entity<InputMoverComponent> entity, ref PlayerAttachedEvent args)
    {
        SetMoveInput(entity, MoveButtons.None);
    }

    private void OnPlayerDetached(Entity<InputMoverComponent> entity, ref PlayerDetachedEvent args)
    {
        SetMoveInput(entity, MoveButtons.None);
    }

    // Mono
    private void OnPilotGetInputs(Entity<PilotComponent> entity, ref GetShuttleInputsEvent args)
    {
        args.GotInput = true;
        args.SetMaxVelocity = entity.Comp.SetMaxVelocity;
        args.SetMaxAngularVelocity = entity.Comp.SetMaxAngularVelocity;

        if (Paused(args.ShuttleUid) || CanPilot(args.ShuttleUid) || !HasComp<PhysicsComponent>(args.ShuttleUid))
            return;

        var input = GetPilotVelocityInput(entity.Comp);
        // don't slow down the ship if we're just looking at the console with zero input
        if (input.Brakes == 0f && input.Rotation == 0f && input.Strafe.LengthSquared() == 0f)
            return;

        var strafe = input.Strafe;
        if (entity.Comp.Console is { } consoleEnt)
        {
            if (TryComp<DroneConsoleComponent>(consoleEnt, out var cargoConsole) && cargoConsole.Entity is { } droneEnt)
                consoleEnt = droneEnt;

            if (TryComp(consoleEnt, out TransformComponent? consoleXform))
                strafe = consoleXform.LocalRotation.RotateVec(strafe);
        }

        args.Input = new ShuttleInput(strafe, input.Rotation, input.Brakes);
    }

    protected override bool CanSound()
    {
        return true;
    }

    private HashSet<EntityUid> _moverAdded = new();
    private List<Entity<InputMoverComponent>> _movers = new();

    private void InsertMover(Entity<InputMoverComponent> source)
    {
        if (TryComp(source, out MovementRelayTargetComponent? relay))
        {
            if (TryComp(relay.Source, out InputMoverComponent? relayMover))
            {
                InsertMover((relay.Source, relayMover));
            }
        }

        // Already added
        if (!_moverAdded.Add(source.Owner))
            return;

        _movers.Add(source);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        _moverAdded.Clear();
        _movers.Clear();
        var inputQueryEnumerator = AllEntityQuery<InputMoverComponent>();

        // Need to order mob movement so that movers don't run before their relays.
        while (inputQueryEnumerator.MoveNext(out var uid, out var mover))
        {
            if (IsPaused(uid) && !HasComp<GhostComponent>(uid)) // Frontier: Skip processing paused entities. Ghosts are excepted for mapping reasons
                continue; // Frontier

            InsertMover((uid, mover));
        }

        foreach (var mover in _movers)
        {
            HandleMobMovement(mover, frameTime);
        }

        ActiveMoverGauge.Set(_movers.Count);

        HandleShuttleMovement(frameTime);

        // Allow non-player pilots (e.g. NPC/HTN steering) to provide shuttle inputs.
        HandlePilotedShuttleMovement(frameTime);
    }

    public (Vector2 Strafe, float Rotation, float Brakes) GetPilotVelocityInput(PilotComponent component)
    {
        if (!Timing.InSimulation)
        {
            // Outside of simulation we'll be running client predicted movement per-frame.
            // So return a full-length vector as if it's a full tick.
            // Physics system will have the correct time step anyways.
            ResetSubtick(component);
            ApplyTick(component, 1f);
            return (component.CurTickStrafeMovement, component.CurTickRotationMovement, component.CurTickBraking);
        }

        float remainingFraction;

        if (Timing.CurTick > component.LastInputTick)
        {
            component.CurTickStrafeMovement = Vector2.Zero;
            component.CurTickRotationMovement = 0f;
            component.CurTickBraking = 0f;
            remainingFraction = 1;
        }
        else
        {
            remainingFraction = (ushort.MaxValue - component.LastInputSubTick) / (float) ushort.MaxValue;
        }

        ApplyTick(component, remainingFraction);

        // Logger.Info($"{curDir}{walk}{sprint}");
        return (component.CurTickStrafeMovement, component.CurTickRotationMovement, component.CurTickBraking);
    }

    private void ResetSubtick(PilotComponent component)
    {
        if (Timing.CurTick <= component.LastInputTick) return;

        component.CurTickStrafeMovement = Vector2.Zero;
        component.CurTickRotationMovement = 0f;
        component.CurTickBraking = 0f;
        component.LastInputTick = Timing.CurTick;
        component.LastInputSubTick = 0;
    }

    protected override void HandleShuttleInput(EntityUid uid, ShuttleButtons button, ushort subTick, bool state)
    {
        if (!TryComp<PilotComponent>(uid, out var pilot) || pilot.Console == null)
            return;

        ResetSubtick(pilot);

        if (subTick >= pilot.LastInputSubTick)
        {
            var fraction = (subTick - pilot.LastInputSubTick) / (float) ushort.MaxValue;

            ApplyTick(pilot, fraction);
            pilot.LastInputSubTick = subTick;
        }

        var buttons = pilot.HeldButtons;

        if (state)
        {
            buttons |= button;
        }
        else
        {
            buttons &= ~button;
        }

        pilot.HeldButtons = buttons;
    }

    private static void ApplyTick(PilotComponent component, float fraction)
    {
        var x = 0;
        var y = 0;
        var rot = 0;
        int brake;

        if ((component.HeldButtons & ShuttleButtons.StrafeLeft) != 0x0)
        {
            x -= 1;
        }

        if ((component.HeldButtons & ShuttleButtons.StrafeRight) != 0x0)
        {
            x += 1;
        }

        component.CurTickStrafeMovement.X += x * fraction;

        if ((component.HeldButtons & ShuttleButtons.StrafeUp) != 0x0)
        {
            y += 1;
        }

        if ((component.HeldButtons & ShuttleButtons.StrafeDown) != 0x0)
        {
            y -= 1;
        }

        component.CurTickStrafeMovement.Y += y * fraction;

        if ((component.HeldButtons & ShuttleButtons.RotateLeft) != 0x0)
        {
            rot -= 1;
        }

        if ((component.HeldButtons & ShuttleButtons.RotateRight) != 0x0)
        {
            rot += 1;
        }

        component.CurTickRotationMovement += rot * fraction;

        if ((component.HeldButtons & ShuttleButtons.Brake) != 0x0)
        {
            brake = 1;
        }
        else
        {
            brake = 0;
        }

        component.CurTickBraking += brake * fraction;
    }

    #region Mono helpers
    /// <summary>
    /// Get a shuttle's angular acceleration.
    /// </summary>
    public float GetAngularAcceleration(ShuttleComponent shuttle, PhysicsComponent body)
    {
        return shuttle.AngularThrust * body.InvI;
    }

    /// <summary>
    /// Get shuttle thrust in a given direction.
    /// Takes local direction.
    /// </summary>
    public Vector2 GetDirectionThrust(Vector2 dir, ShuttleComponent shuttle, PhysicsComponent body)
    {
        if (dir.Length() == 0f)
            return Vector2.Zero;

        dir.Normalize();

        var horizIndex = dir.X > 0 ? 1 : 3; // east else west
        var vertIndex = dir.Y > 0 ? 2 : 0; // north else south
        var horizThrust = shuttle.LinearThrust[horizIndex];
        var vertThrust = shuttle.LinearThrust[vertIndex];

        var horizScale = MathF.Abs(horizThrust / dir.X);
        var vertScale = MathF.Abs(vertThrust / dir.Y);
        // prevent NaNs
        dir *= dir.X == 0 ? vertScale : dir.Y == 0 ? horizScale : MathF.Min(horizScale, vertScale);

        return dir;
    }
    #endregion

    /// <summary>
    /// Helper function to extrapolate max velocity for a given Vector2 (really, its angle) and shuttle.
    /// </summary>
    private Vector2 ObtainMaxVel(Vector2 vel, ShuttleComponent shuttle)
    {
        if (vel.Length() == 0f)
            return Vector2.Zero;

        // this math could PROBABLY be simplified for performance
        // probably
        //             __________________________________
        //            / /    __   __ \2   /    __   __ \2
        // O = I : _ /  |I * | 1/H | |  + |I * |  0  | |
        //          V   \    |_ 0 _| /    \    |_1/V_| /

        var horizIndex = vel.X > 0 ? 1 : 3; // east else west
        var vertIndex = vel.Y > 0 ? 2 : 0; // north else south
        var horizComp = vel.X != 0 ? MathF.Pow(Vector2.Dot(vel, new (shuttle.BaseLinearThrust[horizIndex] / shuttle.LinearThrust[horizIndex], 0f)), 2) : 0; // Frontier: LinearThrust<BaseLinearThrust
        var vertComp = vel.Y != 0 ? MathF.Pow(Vector2.Dot(vel, new (0f, shuttle.BaseLinearThrust[vertIndex] / shuttle.LinearThrust[vertIndex])), 2) : 0; // Frontier: LinearThrust<BaseLinearThrust

        return shuttle.BaseMaxLinearVelocity * vel * MathF.ReciprocalSqrtEstimate(horizComp + vertComp);
    }

    private void HandleShuttleMovement(float frameTime)
    {
        var newPilots = new Dictionary<EntityUid, (ShuttleComponent Shuttle, List<(EntityUid PilotUid, PilotComponent Pilot, InputMoverComponent Mover, TransformComponent ConsoleXform)>)>();

        // We just mark off their movement and the shuttle itself does its own movement
        var activePilotQuery = EntityQueryEnumerator<PilotComponent, InputMoverComponent>();
        while (activePilotQuery.MoveNext(out var uid, out var pilot, out var mover))
        {
            var consoleEnt = pilot.Console;

            // TODO: This is terrible. Just make a new mover and also make it remote piloting + device networks
            if (TryComp<DroneConsoleComponent>(consoleEnt, out var cargoConsole))
            {
                consoleEnt = cargoConsole.Entity;
            }

            if (!TryComp(consoleEnt, out TransformComponent? xform)) continue;

            var gridId = _tablet.GetTabletGrid(consoleEnt) ?? xform.GridUid; // Lua
            // This tries to see if the grid is a shuttle and if the console should work.
            if (!TryComp<MapGridComponent>(gridId, out var _) ||
                !_shuttleQuery.TryGetComponent(gridId, out var shuttleComponent) ||
                !shuttleComponent.Enabled)
                continue;

            if (!newPilots.TryGetValue(gridId!.Value, out var pilots))
            {
                pilots = (shuttleComponent, new List<(EntityUid, PilotComponent, InputMoverComponent, TransformComponent)>());
                newPilots[gridId.Value] = pilots;
            }

            pilots.Item2.Add((uid, pilot, mover, xform));
        }

        // Reset inputs for non-piloted shuttles.
        foreach (var (shuttleUid, (shuttle, _)) in _shuttlePilots)
        {
            if (newPilots.ContainsKey(shuttleUid) || CanPilot(shuttleUid))
                continue;

            _thruster.DisableLinearThrusters(shuttle);
        }

        _shuttlePilots = newPilots;

        foreach (var (shuttleUid, (_, pilots)) in _shuttlePilots)
        {
            foreach (var (pilotUid, _, _, _) in pilots)
            {
                AddPilot(shuttleUid, pilotUid);
            }
        }
    }

    private void PilotedShuttleRelayEvent<TEvent>(Entity<PilotedShuttleComponent> entity, ref TEvent args)
    {
        foreach (var pilot in entity.Comp.InputSources)
        {
            var relayEv = new PilotedShuttleRelayedEvent<TEvent>(args);
            RaiseLocalEvent(pilot, ref relayEv);
        }
    }

    /// <summary>
    /// Registers an entity as an input source for a shuttle.
    /// Used by systems that want to drive a shuttle without a player pilot (e.g. ShipSteeringSystem).
    /// </summary>
    public void AddPilot(EntityUid shuttleUid, EntityUid pilot)
    {
        var piloted = EnsureComp<PilotedShuttleComponent>(shuttleUid);
        piloted.InputSources.Add(pilot);
    }

    private void HandlePilotedShuttleMovement(float frameTime)
    {
        var shuttleQuery = EntityQueryEnumerator<ShuttleComponent, PilotedShuttleComponent, PhysicsComponent>();
        while (shuttleQuery.MoveNext(out var uid, out var shuttle, out var piloted, out var body))
        {
            if (Paused(uid) || CanPilot(uid))
                continue;

            var inputs = new List<ShuttleInput>();
            var toRemove = new List<EntityUid>();

            float setMaxVelSum = 0f;
            float setMaxAngularSum = 0f;
            var setMaxVelCount = 0;
            var setMaxAngularCount = 0;

            foreach (var pilot in piloted.InputSources)
            {
                var inputsEv = new GetShuttleInputsEvent(frameTime, uid);
                RaiseLocalEvent(pilot, ref inputsEv);

                if (!inputsEv.GotInput)
                {
                    toRemove.Add(pilot);
                    continue;
                }

                if (inputsEv.Input != null)
                    inputs.Add(inputsEv.Input.Value);

                if (inputsEv.SetMaxVelocity is { } maxVel)
                {
                    setMaxVelSum += maxVel;
                    setMaxVelCount++;
                }

                if (inputsEv.SetMaxAngularVelocity is { } maxAng)
                {
                    setMaxAngularSum += maxAng;
                    setMaxAngularCount++;
                }
            }

            foreach (var remUid in toRemove)
            {
                piloted.InputSources.Remove(remUid);
            }

            var count = inputs.Count;
            piloted.ActiveSources = count;
            if (count == 0)
            {
                _thruster.DisableLinearThrusters(shuttle);
                PhysicsSystem.SetSleepingAllowed(uid, body, true);
                continue;
            }

            PhysicsSystem.SetSleepingAllowed(uid, body, false);

            // Average all controllers.
            var linearInput = Vector2.Zero;
            var brakeInput = 0f;
            var angularInput = 0f;
            foreach (var inp in inputs)
            {
                linearInput += inp.Strafe.LengthSquared() > 1f ? inp.Strafe.Normalized() : inp.Strafe;
                angularInput += Math.Clamp(inp.Rotation, -1f, 1f);
                brakeInput += MathF.Min(inp.Brakes, 1f);
            }

            linearInput /= count;
            angularInput /= count;
            brakeInput /= count;
            float? setMaxVel = setMaxVelCount > 0 ? setMaxVelSum / setMaxVelCount : null;
            float? setMaxAngular = setMaxAngularCount > 0 ? setMaxAngularSum / setMaxAngularCount : null;

            var shuttleNorthAngle = _xformSystem.GetWorldRotation(uid);

            // Braking
            if (brakeInput > 0f)
            {
                if (body.LinearVelocity.Length() > 0f)
                {
                    const float appearanceThreshold = 0.1f;

                    var shuttleVelocity = (-shuttleNorthAngle).RotateVec(body.LinearVelocity);
                    var force = Vector2.Zero;

                    if (shuttleVelocity.X < 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.West);

                        if (shuttleVelocity.X < -appearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.East);

                        var index = (int) Math.Log2((int) DirectionFlag.East);
                        force.X += shuttle.LinearThrust[index];
                    }
                    else if (shuttleVelocity.X > 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.East);

                        if (shuttleVelocity.X > appearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.West);

                        var index = (int) Math.Log2((int) DirectionFlag.West);
                        force.X -= shuttle.LinearThrust[index];
                    }

                    if (shuttleVelocity.Y < 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.South);

                        if (shuttleVelocity.Y < -appearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.North);

                        var index = (int) Math.Log2((int) DirectionFlag.North);
                        force.Y += shuttle.LinearThrust[index];
                    }
                    else if (shuttleVelocity.Y > 0f)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, DirectionFlag.North);

                        if (shuttleVelocity.Y > appearanceThreshold)
                            _thruster.EnableLinearThrustDirection(shuttle, DirectionFlag.South);

                        var index = (int) Math.Log2((int) DirectionFlag.South);
                        force.Y -= shuttle.LinearThrust[index];
                    }

                    var impulse = force * brakeInput * ShuttleComponent.BrakeCoefficient * _nebulaEnvironment.GetThrustMultiplier(uid);
                    impulse = shuttleNorthAngle.RotateVec(impulse);
                    var forceMul = frameTime * body.InvMass;
                    var maxVelocity = (-body.LinearVelocity).Length() / forceMul;

                    if (impulse.Length() > maxVelocity)
                        impulse = impulse.Normalized() * maxVelocity;

                    PhysicsSystem.ApplyForce(uid, impulse, body: body);
                }
                else
                {
                    _thruster.DisableLinearThrusters(shuttle);
                }

                if (body.AngularVelocity != 0f)
                {
                    var torque = shuttle.AngularThrust * brakeInput * (body.AngularVelocity > 0f ? -1f : 1f) * ShuttleComponent.BrakeCoefficient;
                    var torqueMul = body.InvI * frameTime;

                    if (body.AngularVelocity > 0f)
                        torque = MathF.Max(-body.AngularVelocity / torqueMul, torque);
                    else
                        torque = MathF.Min(-body.AngularVelocity / torqueMul, torque);

                    if (!torque.Equals(0f))
                    {
                        PhysicsSystem.ApplyTorque(uid, torque, body: body);
                        _thruster.SetAngularThrust(shuttle, true);
                    }
                }
                else
                {
                    _thruster.SetAngularThrust(shuttle, false);
                }
            }

            // Linear movement
            if (linearInput.Length().Equals(0f))
            {
                PhysicsSystem.SetSleepingAllowed(uid, body, true);

                if (brakeInput.Equals(0f))
                    _thruster.DisableLinearThrusters(shuttle);
            }
            else
            {
                PhysicsSystem.SetSleepingAllowed(uid, body, false);
                var angle = linearInput.ToWorldAngle();
                var linearDir = angle.GetDir();
                var dockFlag = linearDir.AsFlag();
                var totalForce = Vector2.Zero;

                foreach (DirectionFlag dir in Enum.GetValues(typeof(DirectionFlag)))
                {
                    switch (dir)
                    {
                        case DirectionFlag.South:
                        case DirectionFlag.East:
                        case DirectionFlag.North:
                        case DirectionFlag.West:
                            break;
                        default:
                            continue;
                    }

                    if ((dir & dockFlag) == 0x0)
                    {
                        _thruster.DisableLinearThrustDirection(shuttle, dir);
                        continue;
                    }

                    var force = Vector2.Zero;
                    var index = (int) Math.Log2((int) dir);
                    var thrust = shuttle.LinearThrust[index];

                    switch (dir)
                    {
                        case DirectionFlag.North:
                            force.Y += thrust;
                            break;
                        case DirectionFlag.South:
                            force.Y -= thrust;
                            break;
                        case DirectionFlag.East:
                            force.X += thrust;
                            break;
                        case DirectionFlag.West:
                            force.X -= thrust;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Attempted to apply thrust to shuttle {uid} along invalid dir {dir}.");
                    }

                    _thruster.EnableLinearThrustDirection(shuttle, dir);
                    var impulse = force * linearInput.Length();
                    totalForce += impulse;
                }

                totalForce *= _nebulaEnvironment.GetThrustMultiplier(uid);

                var forceMul = frameTime * body.InvMass;

                var localVel = (-shuttleNorthAngle).RotateVec(body.LinearVelocity);
                var maxVelocity = ObtainMaxVel(localVel, shuttle);
                var maxWishVelocity = ObtainMaxVel(totalForce, shuttle);

                if (setMaxVel is { } speed)
                {
                    if (maxVelocity.LengthSquared() > 0f)
                        maxVelocity = maxVelocity.Normalized() * MathF.Min(maxVelocity.Length(), speed);
                    if (maxWishVelocity.LengthSquared() > 0f)
                        maxWishVelocity = maxWishVelocity.Normalized() * MathF.Min(maxWishVelocity.Length(), speed);
                }

                var properAccel = (maxWishVelocity - localVel) / forceMul;

                var finalForce = Vector2Dot(totalForce, properAccel.Normalized()) * properAccel.Normalized();

                if (localVel.Length() >= maxVelocity.Length() && Vector2.Dot(totalForce, localVel) > 0f)
                    finalForce -= Vector2.Dot(totalForce, localVel.Normalized()) * localVel.Normalized();

                if (finalForce.Length() > properAccel.Length())
                    finalForce = properAccel;

                finalForce = shuttleNorthAngle.RotateVec(finalForce);

                if (finalForce.Length() > 0f)
                    PhysicsSystem.ApplyForce(uid, finalForce, body: body);
            }

            // Angular movement
            if (MathHelper.CloseTo(angularInput, 0f))
            {
                PhysicsSystem.SetSleepingAllowed(uid, body, true);

                if (brakeInput <= 0f)
                    _thruster.SetAngularThrust(shuttle, false);
            }
            else
            {
                PhysicsSystem.SetSleepingAllowed(uid, body, false);
                var torque = shuttle.AngularThrust * -angularInput;

                var torqueMul = body.InvI * frameTime;
                var maxAngular = ShuttleComponent.MaxAngularVelocity;
                if (setMaxAngular is { } ang)
                    maxAngular = MathF.Min(maxAngular, MathF.Max(0f, ang));

                torque = Math.Clamp(torque,
                    (-maxAngular - body.AngularVelocity) / torqueMul,
                    (maxAngular - body.AngularVelocity) / torqueMul);

                if (!torque.Equals(0f))
                {
                    PhysicsSystem.ApplyTorque(uid, torque, body: body);
                    _thruster.SetAngularThrust(shuttle, true);
                }
            }
        }
    }

    // .NET 8 seem to miscompile usage of Vector2.Dot above. This manual outline fixes it pending an upstream fix.
    // See PR #24008
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static float Vector2Dot(Vector2 value1, Vector2 value2)
    {
        return Vector2.Dot(value1, value2);
    }

    private bool CanPilot(EntityUid shuttleUid)
    {
        return TryComp<FTLComponent>(shuttleUid, out var ftl)
        && (ftl.State & (FTLState.Starting | FTLState.Travelling | FTLState.Arriving)) != 0x0
            || HasComp<PreventPilotComponent>(shuttleUid);
    }

}
