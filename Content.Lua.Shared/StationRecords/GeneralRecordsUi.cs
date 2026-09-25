using Robust.Shared.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Serialization;
using Content.Lua.Shared.StationRecords;

namespace Content.Shared.StationRecords;

[Serializable, NetSerializable]
public enum GeneralStationRecordConsoleKey : byte
{
    Key
}

///
///
[Serializable, NetSerializable]
public sealed class GeneralStationRecordConsoleState : BoundUserInterfaceState
{
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
