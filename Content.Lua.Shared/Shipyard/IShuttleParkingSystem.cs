using Robust.Shared.GameObjects;

namespace Content.Lua.Shared.Shipyard;

public interface IShuttleParkingSystem : IEntitySystem
{
    bool IsParked(EntityUid shuttleUid);
    ShuttleParkingResult TryParkShuttle(EntityUid consoleUid, EntityUid shuttleUid);
    ShuttleParkingResult TryRecallShuttle(EntityUid consoleUid, EntityUid shuttleUid, EntityUid targetDockUid);
}

public enum ShuttleParkingError : byte
{
    Success,
    InvalidShuttle,
    AlreadyParked,
    ShuttleNotParked,
    NotDocked,
    OrganicsAboard,
    CryoPodAboard,
    InvalidDock,
    NoDockingPath,
    InvalidConsole,
}

public readonly record struct ShuttleParkingResult(ShuttleParkingError Error, string? OrganicName = null);
