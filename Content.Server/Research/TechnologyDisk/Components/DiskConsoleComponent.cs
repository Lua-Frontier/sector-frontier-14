using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.TechnologyDisk.Components;

[RegisterComponent]
public sealed partial class DiskConsoleComponent : Component
{
    [DataField]
    public List<int> PointDiskAmounts = new() { 1000, 5000, 10000, 25000, 50000 };

    [DataField]
    public Dictionary<int, EntProtoId> PointDiskPrototypes = new()
    {
        [1000] = "ResearchDisk",
        [5000] = "RogueSiliconResearchDisk5000",
        [10000] = "RogueSiliconResearchDisk10000",
        [25000] = "RogueSiliconResearchDisk25000",
        [50000] = "RogueSiliconResearchDisk50000",
    };
    [DataField("diskPrototype"), ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId DiskPrototype = "ResearchDisk";

    [DataField("printDuration"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan PrintDuration = TimeSpan.FromSeconds(1);
    [DataField("printSound")]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
}
