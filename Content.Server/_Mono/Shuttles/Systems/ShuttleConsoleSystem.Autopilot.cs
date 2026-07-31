using System.Numerics;
using Content.Server._Mono.NPC.HTN.Operators;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Mono.Shuttles;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Mono.Shuttles;

public sealed partial class ShuttleConsoleAutopilotSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RadarConsoleSystem _radarConsole = default!;
    [Dependency] private readonly ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleConsoleComponent, ShuttleConsoleAutopilotPositionMessage>(OnAutopilotMessage);
        SubscribeLocalEvent<ShuttleConsoleComponent, SteeringDoneEvent>(OnSteeringDone);
    }

    private void OnAutopilotMessage(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleAutopilotPositionMessage args)
    {
        if (!TryComp<HTNComponent>(ent, out var htn))
            return;

        var blackboard = htn.Blackboard;
        blackboard.SetValue(ent.Comp.AutopilotTargetKey, _transform.ToCoordinates(args.Coordinates));
        blackboard.SetValue(ent.Comp.AutopilotRotationKey, args.Angle + MathF.PI);

        // Show destination on Nav radar via existing Frontier target marker.
        SetAutopilotNavTarget(ent, args.Coordinates.Position);
    }

    private void OnSteeringDone(Entity<ShuttleConsoleComponent> ent, ref SteeringDoneEvent args)
    {
        _audio.PlayPvs(ent.Comp.AutopilotDoneSound, ent);
        _popup.PopupEntity(Loc.GetString("shuttle-console-autopilot-popup-done"), ent, PopupType.Medium);
        ClearAutopilotNavTarget(ent);
    }

    private void SetAutopilotNavTarget(EntityUid console, Vector2 mapPosition)
    {
        if (!TryComp<RadarConsoleComponent>(console, out var radar))
            return;

        _radarConsole.SetTarget((console, radar), NetEntity.Invalid, mapPosition);
        _radarConsole.SetHideTarget((console, radar), false);

        if (Transform(console).GridUid is { } gridUid)
            _shuttleConsole.RefreshShuttleConsoles(gridUid);
    }

    private void ClearAutopilotNavTarget(EntityUid console)
    {
        if (!TryComp<RadarConsoleComponent>(console, out var radar))
            return;

        _radarConsole.ClearTarget((console, radar));

        if (Transform(console).GridUid is { } gridUid)
            _shuttleConsole.RefreshShuttleConsoles(gridUid);
    }
}
