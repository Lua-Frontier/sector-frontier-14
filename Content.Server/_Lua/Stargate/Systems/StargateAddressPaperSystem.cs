// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Server._Lua.Stargate.Components;
using Content.Shared._Lua.Stargate;
using Content.Shared.Paper;
using System.Text;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateAddressPaperSystem : EntitySystem
{
    [Dependency] private readonly StargateAddressRegistrySystem _registry = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StargateAddressPaperComponent, MapInitEvent>(OnAddressPaperMapInit);
        SubscribeLocalEvent<StargateDebugPaperComponent, MapInitEvent>(OnDebugPaperMapInit);
    }

    private void OnAddressPaperMapInit(EntityUid uid, StargateAddressPaperComponent comp, MapInitEvent args)
    {
        if (!TryComp<PaperComponent>(uid, out var paper))
            return;

        var address = _registry.GetRandomPoolAddress();
        if (address == null)
            return;

        _paper.SetContent((uid, paper), FormatAddressGlyphs(address));
    }

    private const int DebugPaperMaxAddresses = 40;

    private void OnDebugPaperMapInit(EntityUid uid, StargateDebugPaperComponent comp, MapInitEvent args)
    {
        if (!TryComp<PaperComponent>(uid, out var paper))
            return;

        var poolSize = _registry.GetPoolSize();
        if (poolSize == 0)
            return;

        var count = Math.Min(DebugPaperMaxAddresses, poolSize);
        var usedKeys = new HashSet<string>();
        var sb = new StringBuilder();

        var idx = 0;
        var attempts = 0;
        while (idx < count && attempts < count * 10)
        {
            attempts++;
            var address = _registry.GetRandomPoolAddress();
            if (address == null)
                break;

            var key = string.Join("-", address);
            if (!usedKeys.Add(key))
                continue;

            idx++;
            sb.AppendLine($"{idx}. {FormatAddressGlyphs(address)}");
        }

        _paper.SetContent((uid, paper), sb.ToString());
    }
    private static string FormatAddressGlyphs(byte[] address)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append("[stargate size=20]");
        for (var i = 0; i < address.Length; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(StargateGlyphs.GetChar(address[i]));
        }
        sb.Append("[/stargate]");
        return sb.ToString();
    }
}
