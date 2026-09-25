using Robust.Shared.Prototypes;

namespace Content.Server.Research.TechnologyDisk.Components;

[RegisterComponent]
public sealed partial class DiskConsolePrintingComponent : Component
{
    public TimeSpan FinishTime;

    public int Points;

    public EntProtoId DiskPrototype = "ResearchDisk";
}
