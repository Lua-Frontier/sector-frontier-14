using Robust.Shared.Serialization;

namespace Content.Shared.Research;

[Serializable, NetSerializable]
public enum DiskConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class DiskConsolePointOption
{
    public int Points;
    public bool CanPrint;

    public DiskConsolePointOption()
    {
    }

    public DiskConsolePointOption(int points, bool canPrint)
    {
        Points = points;
        CanPrint = canPrint;
    }
}

[Serializable, NetSerializable]
public sealed class DiskConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public int ServerPoints;
    public bool Printing;
    public List<DiskConsolePointOption> Options = new();

    public DiskConsoleBoundUserInterfaceState(int serverPoints, bool printing, List<DiskConsolePointOption> options)
    {
        ServerPoints = serverPoints;
        Printing = printing;
        Options = options;
    }
}

[Serializable, NetSerializable]
public sealed class DiskConsolePrintDiskMessage : BoundUserInterfaceMessage
{
    public int Points;

    public DiskConsolePrintDiskMessage(int points)
    {
        Points = points;
    }
}
