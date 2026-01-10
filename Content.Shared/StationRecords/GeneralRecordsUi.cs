using Robust.Shared.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Serialization;
using Content.Shared._Lua.StationRecords;

namespace Content.Shared.StationRecords;

[Serializable, NetSerializable]
public enum GeneralStationRecordConsoleKey : byte
{
    Key
}

/// <summary>
///     General station records console state. There are a few states:
///     - SelectedKey null, Record null, RecordListing null
///         - The station record database could not be accessed.
///     - SelectedKey null, Record null, RecordListing non-null
///         - Records are populated in the database, or at least the station has
///           the correct component.
///     - SelectedKey non-null, Record null, RecordListing non-null
///         - The selected key does not have a record tied to it.
///     - SelectedKey non-null, Record non-null, RecordListing non-null
///         - The selected key has a record tied to it, and the record has been sent.
///
///     - there is added new filters and so added new states
///         -SelectedKey null, Record null, RecordListing null, filters non-null
///            the station may have data, but they all did not pass through the filters
///
///     Other states are erroneous.
/// </summary>
[Serializable, NetSerializable]
public sealed class GeneralStationRecordConsoleState : BoundUserInterfaceState
{
    /// <summary>
    /// Current selected key.
    /// Station is always the station that owns the console.
    /// </summary>
    public readonly uint? SelectedKey;
    public readonly GeneralStationRecord? Record;
    public readonly Dictionary<uint, string>? RecordListing;
    public IReadOnlyDictionary<ProtoId<JobPrototype>, int?>? JobList { get; } // Frontier
    public readonly StationRecordsFilter? Filter;
    public readonly bool CanDeleteEntries;
    public readonly string? Advertisement; // Frontier
    public readonly bool IsCaptainIdPresent;
    public readonly string? CaptainIdName;
    public readonly string? CaptainShipName;
    public readonly bool IsTargetIdPresent;
    public readonly string? TargetIdName;
    public readonly string? TargetAssignedShipName;
    public readonly string? TargetAssignedRoleLocKey;
    public readonly List<ShipCrewRosterEntry>? ShipCrewRoster;

    public GeneralStationRecordConsoleState(uint? key, GeneralStationRecord? record, Dictionary<uint, string>? recordListing, IReadOnlyDictionary<ProtoId<JobPrototype>, int?>? jobList, StationRecordsFilter? newFilter, bool canDeleteEntries, string? advertisement, bool isCaptainIdPresent, string? captainIdName, string? captainShipName, bool isTargetIdPresent, string? targetIdName, string? targetAssignedShipName, string? targetAssignedRoleLocKey, List<ShipCrewRosterEntry>? shipCrewRoster)
    {
        SelectedKey = key;
        Record = record;
        RecordListing = recordListing;
        Filter = newFilter;
        JobList = jobList; // Frontier
        CanDeleteEntries = canDeleteEntries;
        Advertisement = advertisement; // Frontier
        IsCaptainIdPresent = isCaptainIdPresent;
        CaptainIdName = captainIdName;
        CaptainShipName = captainShipName;
        IsTargetIdPresent = isTargetIdPresent;
        TargetIdName = targetIdName;
        TargetAssignedShipName = targetAssignedShipName;
        TargetAssignedRoleLocKey = targetAssignedRoleLocKey;
        ShipCrewRoster = shipCrewRoster;
    }

    public GeneralStationRecordConsoleState() : this(null, null, null, null, null, false, string.Empty, false, null, null, false, null, null, null, null)
    {
    }

    public bool IsEmpty() => SelectedKey == null
        && Record == null && RecordListing == null;
}

/// <summary>
/// Select a specific crewmember's record, or deselect.
/// Used by any kind of records console including general and criminal.
/// </summary>
[Serializable, NetSerializable]
public sealed class SelectStationRecord : BoundUserInterfaceMessage
{
    public readonly uint? SelectedKey;

    public SelectStationRecord(uint? selectedKey)
    {
        SelectedKey = selectedKey;
    }
}


[Serializable, NetSerializable]
public sealed class DeleteStationRecord : BoundUserInterfaceMessage
{
    public DeleteStationRecord(uint id)
    {
        Id = id;
    }

    public readonly uint Id;
}
