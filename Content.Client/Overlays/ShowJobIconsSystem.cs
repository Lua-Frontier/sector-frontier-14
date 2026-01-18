using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared._Lua.StationRecords.Components;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using Robust.Client.Player;

namespace Content.Client.Overlays;

public sealed class ShowJobIconsSystem : EquipmentHudSystem<ShowJobIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private static readonly ProtoId<JobIconPrototype> JobIconForNoId = "JobIconNoId";
    private static readonly ProtoId<JobIconPrototype> JobIconTeam = "JobIconTeam"; // Lua
    private static readonly ProtoId<JobIconPrototype> JobIconTeamCaptain = "JobIconTeamCaptain"; // Lua

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusIconComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, StatusIconComponent _, ref GetStatusIconsEvent ev)
    {
        if (TryGetViewerShip(out var viewerShip) && TryGetTeamStatus(uid, viewerShip, out var isCaptain, out var isCrew))
        {
            AddJobIcon(uid, ref ev);
            if (isCaptain && _prototype.TryIndex(JobIconTeamCaptain, out var captainIcon)) ev.StatusIcons.Add(captainIcon);
            else if (isCrew && _prototype.TryIndex(JobIconTeam, out var crewIcon)) ev.StatusIcons.Add(crewIcon);
            return;
        }
        if (!IsActive) return;
        AddJobIcon(uid, ref ev);
    }

    private void AddJobIcon(EntityUid uid, ref GetStatusIconsEvent ev)
    {
        var iconId = JobIconForNoId;

        if (_accessReader.FindAccessItemsInventory(uid, out var items))
        {
            foreach (var item in items)
            {
                // ID Card
                if (TryComp<IdCardComponent>(item, out var id))
                {
                    iconId = id.JobIcon;
                    break;
                }

                // PDA
                if (TryComp<PdaComponent>(item, out var pda)
                    && pda.ContainedId != null
                    && TryComp(pda.ContainedId, out id))
                {
                    iconId = id.JobIcon;
                    break;
                }
            }
        }

        if (_prototype.TryIndex(iconId, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
        else
            Log.Error($"Invalid job icon prototype: {iconPrototype}");
    }

    // Lua team icon mod start
    private bool TryGetViewerShip(out EntityUid shipUid)
    {
        shipUid = default;

        if (_playerManager.LocalEntity is not { Valid: true } viewer)
            return false;

        return TryGetShipFromInventory(viewer, out shipUid);
    }

    private bool TryGetTeamStatus(EntityUid target, EntityUid viewerShip, out bool isCaptain, out bool isCrew)
    {
        isCaptain = false;
        isCrew = false;

        if (!_accessReader.FindAccessItemsInventory(target, out var items))
            return false;

        foreach (var item in items)
        {
            if (TryCheckIdEntity(item, viewerShip, ref isCaptain, ref isCrew)) continue;
            if (TryComp<PdaComponent>(item, out var pda) && pda.ContainedId is { Valid: true } containedId) TryCheckIdEntity(containedId, viewerShip, ref isCaptain, ref isCrew);
        }
        return isCaptain || isCrew;
    }

    private bool TryGetShipFromInventory(EntityUid entity, out EntityUid shipUid)
    {
        shipUid = default;
        if (!_accessReader.FindAccessItemsInventory(entity, out var items)) return false;
        foreach (var item in items)
        {
            if (TryGetShipFromIdEntity(item, out shipUid)) return true;
            if (TryComp<PdaComponent>(item, out var pda) && pda.ContainedId is { Valid: true } containedId)
            { if (TryGetShipFromIdEntity(containedId, out shipUid)) return true; }
        }
        return false;
    }

    private bool TryGetShipFromIdEntity(EntityUid idEntity, out EntityUid shipUid)
    {
        shipUid = default;
        if (TryComp<ShuttleDeedComponent>(idEntity, out var deed) && deed.ShuttleUid is { Valid: true } deedShip)
        {
            shipUid = deedShip;
            return true;
        }
        if (TryComp<ShipCrewAssignmentStatusComponent>(idEntity, out var status) && status.ShuttleUid is { Valid: true } assignedShip)
        {
            shipUid = assignedShip;
            return true;
        }
        return false;
    }

    private bool TryCheckIdEntity(EntityUid idEntity, EntityUid viewerShip, ref bool isCaptain, ref bool isCrew)
    {
        if (TryComp<ShuttleDeedComponent>(idEntity, out var deed) && deed.ShuttleUid == viewerShip) isCaptain = true;
        if (TryComp<ShipCrewAssignmentStatusComponent>(idEntity, out var status) && status.ShuttleUid == viewerShip) isCrew = true;
        return isCaptain || isCrew;
    }
    // Lua team icon mod end
}
