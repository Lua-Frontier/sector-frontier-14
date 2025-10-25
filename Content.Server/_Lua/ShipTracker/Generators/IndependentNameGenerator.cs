// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Server.Maps.NameGenerators;
using JetBrains.Annotations;
using Robust.Shared.Random;

namespace Content.Server._Lua.Starmap.Generators;

[UsedImplicitly]
public sealed partial class IndependentNameGenerator : StationNameGenerator
{
    [DataField("prefixCreator")] public string PrefixCreator = default!;

    private string Prefix => "ISV";
    private string[] SuffixCodes => new []{ "LV", "NX", "EV", "QT", "PR" };

    public override string FormatName(string input)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        return string.Format(input, $"{Prefix}-{PrefixCreator}", $"{random.Pick(SuffixCodes)}-{random.Next(0, 999):D3}");
    }
}
