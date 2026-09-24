using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Content.Shared.Research.Discovery;

namespace Content.Lua.Shared.Research.Discovery;

[RegisterComponent, ComponentProtoName("ServerTechReconfigurator")]
public sealed partial class ServerTechReconfiguratorComponent : Component
{
    [DataField]
    public string DiskSlotId = "research_disk_slot";

    [DataField]
    public int PairCount = 12;

    [DataField]
    public int GridSize = 12;

    [DataField]
    public float DownloadDuration = 20f;

    public EntityUid? TargetServer;
    public EntityUid? User;
    public string? SelectedTechnology;
    public bool Downloading;
    public float DownloadProgress;
    public List<ServerTechRoutePair> Pairs = new();
    public Dictionary<int, List<Vector2i>> AcceptedRoutes = new();
    public bool RepairMode;
    public int SessionGridSize;
    public HashSet<int> BrokenRepairPairs = new();
}

[Serializable, NetSerializable]
public enum ServerTechReconfiguratorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ServerTechReconfiguratorBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool HasTarget;
    public string? TargetName;
    public bool TargetPowered;
    public bool HasDisk;
    public ResearchDataDiskStage DiskStage;
    public string? SelectedTechnology;
    public bool PuzzleActive;
    public bool Downloading;
    public float DownloadProgress;
    public float DownloadDuration;
    public int GridSize;
    public bool IsRepairMode;
    public List<DiscoveryUnlockedEntry> Technologies = new();
    public List<ServerTechRoutePair> Pairs = new();
    public List<ServerTechAcceptedRoute> AcceptedRoutes = new();
}

[Serializable, NetSerializable]
public sealed class ServerTechRoutePair
{
    public int Id;
    public Vector2i Start;
    public Vector2i End;

    public ServerTechRoutePair()
    {
    }

    public ServerTechRoutePair(int id, Vector2i start, Vector2i end)
    {
        Id = id;
        Start = start;
        End = end;
    }
}

[Serializable, NetSerializable]
public sealed class ServerTechAcceptedRoute
{
    public int PairId;
    public List<Vector2i> Cells = new();

    public ServerTechAcceptedRoute()
    {
    }

    public ServerTechAcceptedRoute(int pairId, List<Vector2i> cells)
    {
        PairId = pairId;
        Cells = cells;
    }
}

[Serializable, NetSerializable]
public sealed class ServerTechSelectTechnologyMessage : BoundUserInterfaceMessage
{
    public string TechnologyId;

    public ServerTechSelectTechnologyMessage(string technologyId)
    {
        TechnologyId = technologyId;
    }
}

[Serializable, NetSerializable]
public sealed class ServerTechStartTheftMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ServerTechSubmitRouteMessage : BoundUserInterfaceMessage
{
    public int PairId;
    public List<Vector2i> Cells;

    public ServerTechSubmitRouteMessage(int pairId, List<Vector2i> cells)
    {
        PairId = pairId;
        Cells = cells;
    }
}

[Serializable, NetSerializable]
public sealed class ServerTechResetPuzzleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ServerTechEjectDiskMessage : BoundUserInterfaceMessage;
