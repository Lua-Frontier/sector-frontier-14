// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lua.Starmap;

public sealed class ComposedStarmapData
{
    public required string Id { get; init; }
    public required StarDefinition[] Stars { get; init; }
    public required string[][] Hyperlanes { get; init; }
    public required FactionZoneDefinition[] FactionZones { get; init; }
    public required ChartRegionDefinition[] ChartRegions { get; init; }
    public required ChartMarkerDefinition[] ChartMarkers { get; init; }
}

public static class StarmapDataComposer
{
    public static bool TryCompose(
        IPrototypeManager prototypes,
        string dataId,
        [NotNullWhen(true)] out ComposedStarmapData? data)
    {
        data = null;
        if (!prototypes.TryIndex<StarmapDataPrototype>(dataId, out var root))
            return false;

        data = Compose(prototypes, root);
        return true;
    }

    public static ComposedStarmapData Compose(IPrototypeManager prototypes, StarmapDataPrototype root)
    {
        var stars = new Dictionary<string, StarDefinition>(StringComparer.OrdinalIgnoreCase);
        var zones = new Dictionary<string, FactionZoneDefinition>(StringComparer.OrdinalIgnoreCase);
        var markers = new Dictionary<string, ChartMarkerDefinition>(StringComparer.OrdinalIgnoreCase);
        var regions = new Dictionary<string, ChartRegionDefinition>(StringComparer.OrdinalIgnoreCase);
        var hyperlanes = new List<string[]>();
        var seenHyper = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Absorb(StarmapDataPrototype proto)
        {
            foreach (var star in proto.Stars)
            {
                if (!string.IsNullOrEmpty(star.Id))
                    stars[star.Id] = star;
            }

            foreach (var zone in proto.FactionZones)
            {
                if (!string.IsNullOrEmpty(zone.Id))
                    zones[zone.Id] = zone;
            }

            foreach (var marker in proto.ChartMarkers)
            {
                if (!string.IsNullOrEmpty(marker.Id))
                    markers[marker.Id] = marker;
            }

            foreach (var region in proto.ChartRegions)
            {
                if (!string.IsNullOrEmpty(region.Id))
                    regions[region.Id] = region;
            }

            foreach (var lane in proto.Hyperlanes)
            {
                if (lane.Length < 2
                    || string.IsNullOrWhiteSpace(lane[0])
                    || string.IsNullOrWhiteSpace(lane[1]))
                    continue;

                var a = lane[0];
                var b = lane[1];
                var key = string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
                if (seenHyper.Add(key))
                    hyperlanes.Add(lane);
            }
        }

        if (root.Parents != null)
        {
            foreach (var parentId in root.Parents)
            {
                if (prototypes.TryIndex<StarmapDataPrototype>(parentId, out var parent))
                    Absorb(parent);
            }
        }

        Absorb(root);

        return new ComposedStarmapData
        {
            Id = root.ID,
            Stars = stars.Values.ToArray(),
            FactionZones = zones.Values.ToArray(),
            ChartMarkers = markers.Values.ToArray(),
            ChartRegions = regions.Values.ToArray(),
            Hyperlanes = hyperlanes.ToArray(),
        };
    }
}
