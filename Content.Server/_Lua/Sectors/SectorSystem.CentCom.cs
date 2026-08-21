// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server.Backmen.Arrivals.CentComm;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Backmen.Abilities;
using Content.Shared.Backmen.Arrivals;
using Content.Shared.Cargo.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Server._Lua.Sectors;

public sealed partial class SectorSystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private static readonly SoundSpecifier CentComEmagSparkSound = new SoundCollectionSpecifier("sparks");

    [ValidatePrototypeId<EntityPrototype>]
    private const string StationShuttleConsole = "ComputerShuttle";

    [ValidatePrototypeId<GameMapPrototype>]
    private const string StationCentComMapDefault = "CentComm";

    private void InitializeCentComGameplay()
    {
        SubscribeLocalEvent<ActorComponent, CentcomFtlAction>(OnCentComFtlActionUsed);
        SubscribeLocalEvent<PreGameMapLoad>(OnCentComPreGameMapLoad, after: new[] { typeof(StationSystem) });
        SubscribeLocalEvent<RoundEndedEvent>(OnCentComEndRound);
        SubscribeLocalEvent<ShuttleConsoleComponent, GotEmaggedEvent>(OnCentComShuttleConsoleEmagged);
        SubscribeLocalEvent<FTLCompletedEvent>(OnCentComFtlCompleted);
        SubscribeLocalEvent<FtlCentComAnnounce>(OnCentComFtlAnnounce);
    }

    private void OnCentComEndRound(RoundEndedEvent ev)
    {
        UnlockCentComFtl();
    }

    private void OnCentComFtlAnnounce(FtlCentComAnnounce ev)
    {
        if (!TryGetCentCom(out _, out _, out var centComGrid) || !centComGrid.IsValid())
            return;

        var shuttleName = "Неизвестный";
        if ((!TryComp<IFFComponent>(ev.Source, out var iff) || (iff.Flags & IFFFlags.Hide) == 0)
            && !string.IsNullOrWhiteSpace(MetaData(ev.Source).EntityName))
        {
            shuttleName = MetaData(ev.Source).EntityName;
        }

        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        _chat.DispatchStationAnnouncement(centComGrid,
            $"Внимание! Радары обнаружили {shuttleName} шаттл, входящий в космическое пространство объекта Центрального Командования!",
            "Радар", colorOverride: Color.Crimson);
    }

    private void OnCentComFtlCompleted(ref FTLCompletedEvent ev)
    {
        if (!TryGetCentCom(out var mapUid, out _, out _))
            return;

        if (ev.MapUid != mapUid)
            return;

        if (!TryComp<ShuttleComponent>(ev.Entity, out var shuttleComponent))
            return;

        QueueLocalEvent(new FtlCentComAnnounce
        {
            Source = (ev.Entity, shuttleComponent)
        });
    }

    private void OnCentComShuttleConsoleEmagged(Entity<ShuttleConsoleComponent> ent, ref GotEmaggedEvent args)
    {
        if (Prototype(ent)?.ID != StationShuttleConsole)
            return;

        if (!this.IsPowered(ent, EntityManager))
            return;

        var shuttle = Transform(ent).GridUid;
        if (!HasComp<ShuttleComponent>(shuttle))
            return;

        if (!HasComp<CargoShuttleComponent>(shuttle))
            return;

        _audio.PlayPvs(CentComEmagSparkSound, ent);
        _popup.PopupEntity(Loc.GetString("shuttle-console-component-upgrade-emag-requirement"), ent);
        args.Handled = true;
        EnsureComp<AllowFtlToCentComComponent>(shuttle.Value);
        _console.RefreshShuttleConsoles();
    }

    private void OnCentComPreGameMapLoad(PreGameMapLoad ev)
    {
        if (ev.GameMap.ID != StationCentComMapDefault)
            return;

        ev.Options.PauseMaps = false;
        ev.Options.InitializeMaps = true;
        ev.Offset = Vector2.Zero;
        ev.Rotation = Angle.Zero;
    }

    private void OnCentComFtlActionUsed(EntityUid uid, ActorComponent component, CentcomFtlAction args)
    {
        var grid = Transform(args.Performer);
        if (grid.GridUid == null)
            return;

        if (!TryComp<PilotComponent>(args.Performer, out var pilotComponent) || pilotComponent.Console == null)
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-no-pilot"), args.Performer, args.Performer);
            return;
        }

        TransformComponent shuttle;

        if (TryComp<DroneConsoleComponent>(pilotComponent.Console, out var droneConsoleComponent) &&
            droneConsoleComponent.Entity != null)
        {
            shuttle = Transform(droneConsoleComponent.Entity.Value);
        }
        else
        {
            shuttle = grid;
        }

        if (!TryComp<ShuttleComponent>(shuttle.GridUid, out var comp) || HasComp<FTLComponent>(shuttle.GridUid) || (
                HasComp<BecomesStationComponent>(shuttle.GridUid) &&
                !HasComp<CargoShuttleComponent>(shuttle.GridUid)
            ))
        {
            return;
        }

        if (!TryGetCentCom(out _, out _, out var centComGrid) || !centComGrid.IsValid() || Deleted(centComGrid))
        {
            _popup.PopupEntity(Loc.GetString("centcom-ftl-action-no-station"), args.Performer, args.Performer);
            return;
        }

        if (!_shuttle.CanFTL(shuttle.GridUid.Value, out var reason))
        {
            _popup.PopupEntity(reason, args.Performer, args.Performer);
            return;
        }

        _shuttle.FTLToDock(shuttle.GridUid.Value, comp, centComGrid);
    }
}
