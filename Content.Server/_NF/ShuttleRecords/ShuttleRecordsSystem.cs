using System.Diagnostics.CodeAnalysis;
using Content.Server._NF.SectorServices;
using Content.Server._NF.ShuttleRecords.Components;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._NF.ShuttleRecords;
using Content.Shared.Access.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._NF.ShuttleRecords;

public sealed partial class ShuttleRecordsSystem : SharedShuttleRecordsSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeShuttleRecords();
    }

    /**
     * Adds a record to the shuttle records list.
     * <param name="record">The record to add.</param>
     */
    public void AddRecord(ShuttleRecord record)
    {
        if (!TryGetShuttleRecordsDataComponent(out var component))
            return;

        record.TimeOfPurchase = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
        component.ShuttleRecords[record.EntityUid] = record;
        RefreshStateForAll();
    }

    /**
     * Edits an existing record if one exists for the entity given in the Record
     * <param name="record">The record to update.</param>
     */
    public void TryUpdateRecord(ShuttleRecord record)
    {
        if (!TryGetShuttleRecordsDataComponent(out var component))
            return;

        component.ShuttleRecords[record.EntityUid] = record;
        RefreshStateForAll();
    }

    /**
     * Edits an existing record if one exists for the given entity
     * <param name="record">The record to add.</param>
     */
    public bool TryGetRecord(NetEntity uid, [NotNullWhen(true)] out ShuttleRecord? record)
    {
        if (!TryGetShuttleRecordsDataComponent(out var component) ||
            !component.ShuttleRecords.ContainsKey(uid))
        {
            record = null;
            return false;
        }

        record = component.ShuttleRecords[uid];
        return true;
    }

    public bool TrySetSaleTime(NetEntity uid)
    {
        if (TryGetRecord(uid, out var record))
        {
            record.TimeOfSale = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
            TryUpdateRecord(record);
            return true;
        }
        return false;
    }

    public bool TryGetAllRecords([NotNullWhen(true)] out IReadOnlyCollection<ShuttleRecord>? records)
    {
        if (!TryGetShuttleRecordsDataComponent(out var component) || component.ShuttleRecords.Count == 0)
        {
            records = null;
            return false;
        }

        records = component.ShuttleRecords.Values;
        return true;
    }

    private bool TryGetShuttleRecordsDataComponent([NotNullWhen(true)] out SectorShuttleRecordsComponent? component)
    {
        var serviceEntity = _sectorService.GetServiceEntity();
        if (serviceEntity == EntityUid.Invalid || !EntityManager.EntityExists(serviceEntity) || Terminating(serviceEntity))
        {
            component = null;
            return false;
        }
        if (_entityManager.EnsureComponent<SectorShuttleRecordsComponent>(uid: serviceEntity, out var shuttleRecordsComponent))
        {
            component = shuttleRecordsComponent;
            return true;
        }

        component = null;
        return false;
    }
}
