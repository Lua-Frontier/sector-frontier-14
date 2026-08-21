using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared._Mono.ShipGuns;

namespace Content.Shared._Mono.FireControl;

[Serializable, NetSerializable]
public sealed class FireControlConsoleUpdateEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class FireControlConsoleBoundInterfaceState : BoundUserInterfaceState
{
    public bool Connected;
    public FireControllableEntry[] FireControllables;
    public NavInterfaceState NavState;

    public FireControlConsoleBoundInterfaceState(bool connected, FireControllableEntry[] fireControllables, NavInterfaceState navState)
    {
        Connected = connected;
        FireControllables = fireControllables;
        NavState = navState;
    }
}

[Serializable, NetSerializable]
public enum FireControlConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FireControlConsoleRefreshServerMessage : BoundUserInterfaceMessage
{

}

[Serializable, NetSerializable]
public sealed class FireControlConsoleFireMessage : BoundUserInterfaceMessage
{
    public List<NetEntity> Selected;
    public NetCoordinates Coordinates;
    public FireControlConsoleFireMessage(List<NetEntity> selected, NetCoordinates coordinates)
    {
        Selected = selected;
        Coordinates = coordinates;
    }
}

/// <summary>
/// Event raised when a fire control console wants to fire weapons at specific coordinates.
/// Used for tracking cursor position.
/// </summary>
public sealed class FireControlConsoleFireEvent : EntityEventArgs
{
    public NetEntity ConsoleUid;

    /// <summary>
    /// The coordinates of the cursor/firing position
    /// </summary>
    public NetCoordinates Coordinates;

    /// <summary>
    /// The weapons selected to fire
    /// </summary>
    public List<NetEntity> Selected;

    public FireControlConsoleFireEvent(NetEntity consoleUid, NetCoordinates coordinates, List<NetEntity> selected)
    {
        ConsoleUid = consoleUid;
        Coordinates = coordinates;
        Selected = selected;
    }
}

[Serializable, NetSerializable]
public struct FireControllableEntry
{
    /// <summary>
    /// The entity in question
    /// </summary>
    public NetEntity NetEntity;

    /// <summary>
    /// Location of the entity
    /// </summary>
    public NetCoordinates Coordinates;

    /// <summary>
    /// Display name of the entity
    /// </summary>
    public string Name;

    public ShipGunType Type;

    public FireControllableEntry(NetEntity entity, NetCoordinates coordinates, string name, ShipGunType type = ShipGunType.Other)
    {
        NetEntity = entity;
        Coordinates = coordinates;
        Name = name;
        Type = type;
    }
}
