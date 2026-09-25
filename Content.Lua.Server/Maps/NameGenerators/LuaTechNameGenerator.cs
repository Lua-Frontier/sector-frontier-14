using JetBrains.Annotations;
using Robust.Shared.Random;

namespace Content.Server.Maps.NameGenerators;

[UsedImplicitly]
public sealed partial class LuaTechNameGenerator : StationNameGenerator
{
    [DataField("prefixCreator")] public string PrefixCreator = default!;

    private string Prefix => "";

    public override string FormatName(string input)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        return string.Format(input, $"{Prefix}{PrefixCreator}", $"{random.Next(0, 10000):D4}");
    }
}
