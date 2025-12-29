using JetBrains.Annotations;
using Robust.Shared.Random;

namespace Content.Server.Maps.NameGenerators;

[UsedImplicitly]
public sealed partial class LuaTechNameGenerator : StationNameGenerator
{
    [DataField("prefixCreator")] public string PrefixCreator = default!;

    //private string Prefix => "LT";
    private string Prefix => "";
    private string[] SuffixCodes => new []{ "LT" };

    public override string FormatName(string input)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        //return string.Format(input, $"{Prefix}{PrefixCreator}", $"{random.Pick(SuffixCodes)}-{random.Next(0, 1000):D3}");
        return string.Format(input, $"{Prefix}{PrefixCreator}", $"{random.Next(0, 1000):D3}");
    }
}
