// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using System.Globalization;
using System.Linq;
using System.Text;
using Content.Lua.Shared.Shipyard;
using Content.Server._NF.ShuttleRecords;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared._NF.ShuttleRecords;
using Robust.Shared.Prototypes;

namespace Content.Lua.Server.Shipyard.Systems;

public sealed class ShipyardPreferencesReportSystem : EntitySystem, IShipyardPreferencesReportSystem
{
    private const int TopShipsPerCategory = 12;
    private const int DiscordEmbedDescriptionLimit = 3900;

    private const int FallbackNewcomerMax = 100_000;
    private const int FallbackLowMax = 1_000_000;
    private const int FallbackMidMax = 10_000_000;

    [Dependency] private readonly ShuttleRecordsSystem _shuttleRecords = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public string? GetStatsPrintout(IReadOnlyList<int> roundBalances)
    {
        if (!_shuttleRecords.TryGetAllRecords(out var records))
            return null;

        var thresholds = ComputeWealthThresholds(roundBalances, records);

        var categories = new Dictionary<WealthClass, Dictionary<string, RecordSummary>>
        {
            [WealthClass.Newcomers] = new(),
            [WealthClass.Low] = new(),
            [WealthClass.Mid] = new(),
            [WealthClass.High] = new(),
        };

        var totalShips = 0;
        var totalAbandoned = 0;
        var totalLifetimes = new List<TimeSpan>();

        foreach (var record in records)
        {
            if (string.IsNullOrEmpty(record.VesselPrototypeId))
                continue;

            var balance = record.BuyerBalance ?? (int)record.PurchasePrice;
            var wealthClass = ClassifyBalance(balance, thresholds);

            var ships = categories[wealthClass];
            if (!ships.TryGetValue(record.VesselPrototypeId, out var summary))
            {
                summary = new RecordSummary();
                ships[record.VesselPrototypeId] = summary;
            }

            summary.Count += 1;
            totalShips += 1;

            if (EntityManager.TryGetEntity(record.EntityUid, out _))
            {
                summary.AbandonedCount += 1;
                totalAbandoned += 1;
            }

            if (record.TimeOfPurchase is { } purchaseTime && record.TimeOfSale is { } saleTime)
            {
                var lifetime = saleTime.Subtract(purchaseTime);
                summary.Lifetimes.Add(lifetime);
                totalLifetimes.Add(lifetime);
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine(Loc.GetString(
            "adventure-webhook-shipstats-summary",
            ("ships", totalShips),
            ("abandoned", totalAbandoned),
            ("avgTime", FormatAverageLifetime(totalLifetimes))));
        builder.AppendLine();
        builder.AppendLine(Loc.GetString(
            "adventure-webhook-shipstats-thresholds",
            ("newcomers", BankSystemExtensions.ToSpesoString(thresholds.NewcomerMax)),
            ("low", BankSystemExtensions.ToSpesoString(thresholds.LowMax)),
            ("mid", BankSystemExtensions.ToSpesoString(thresholds.MidMax))));

        AppendCategory(builder, WealthClass.Newcomers, categories[WealthClass.Newcomers], thresholds);
        AppendCategory(builder, WealthClass.Low, categories[WealthClass.Low], thresholds);
        AppendCategory(builder, WealthClass.Mid, categories[WealthClass.Mid], thresholds);
        AppendCategory(builder, WealthClass.High, categories[WealthClass.High], thresholds);

        var text = builder.ToString();
        if (text.Length > DiscordEmbedDescriptionLimit)
            text = text[..DiscordEmbedDescriptionLimit] + "\n…";

        return text;
    }

    private void AppendCategory(
        StringBuilder builder,
        WealthClass wealthClass,
        Dictionary<string, RecordSummary> ships,
        WealthThresholds thresholds)
    {
        builder.AppendLine();
        builder.AppendLine(GetCategoryHeader(wealthClass, thresholds));

        if (ships.Count == 0)
        {
            builder.AppendLine(Loc.GetString("adventure-webhook-shipstats-empty"));
            return;
        }

        var sorted = ships
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => GetVesselDisplayName(pair.Key))
            .Take(TopShipsPerCategory);

        foreach (var (prototypeId, summary) in sorted)
        {
            builder.AppendLine(Loc.GetString(
                "adventure-webhook-shipstats-entry",
                ("count", summary.Count),
                ("ship", GetVesselDisplayName(prototypeId)),
                ("avgTime", FormatAverageLifetime(summary.Lifetimes)),
                ("abandoned", summary.AbandonedCount)));
        }
    }

    private string GetCategoryHeader(WealthClass wealthClass, WealthThresholds thresholds)
    {
        return wealthClass switch
        {
            WealthClass.Newcomers => Loc.GetString(
                "adventure-webhook-shipstats-category-newcomers",
                ("max", BankSystemExtensions.ToSpesoString(thresholds.NewcomerMax))),
            WealthClass.Low => Loc.GetString(
                "adventure-webhook-shipstats-category-low",
                ("min", BankSystemExtensions.ToSpesoString(thresholds.NewcomerMax + 1)),
                ("max", BankSystemExtensions.ToSpesoString(thresholds.LowMax))),
            WealthClass.Mid => Loc.GetString(
                "adventure-webhook-shipstats-category-mid",
                ("min", BankSystemExtensions.ToSpesoString(thresholds.LowMax + 1)),
                ("max", BankSystemExtensions.ToSpesoString(thresholds.MidMax))),
            _ => Loc.GetString(
                "adventure-webhook-shipstats-category-high",
                ("min", BankSystemExtensions.ToSpesoString(thresholds.MidMax + 1))),
        };
    }

    private string GetVesselDisplayName(string prototypeId)
    {
        if (_prototype.TryIndex<VesselPrototype>(prototypeId, out var vessel) &&
            !string.IsNullOrWhiteSpace(vessel.Name))
        {
            return Loc.TryGetString(vessel.Name, out var localized) ? localized : vessel.Name;
        }

        return prototypeId;
    }

    private static WealthThresholds ComputeWealthThresholds(
        IReadOnlyList<int> roundBalances,
        IEnumerable<ShuttleRecord> records)
    {
        var balances = roundBalances
            .Where(balance => balance >= 0)
            .ToList();

        if (balances.Count == 0)
        {
            balances = records
                .Where(record => record.BuyerBalance is >= 0)
                .Select(record => record.BuyerBalance!.Value)
                .ToList();
        }

        if (balances.Count == 0)
            return new WealthThresholds(FallbackNewcomerMax, FallbackLowMax, FallbackMidMax);

        balances.Sort();
        return new WealthThresholds(
            Percentile(balances, 0.25),
            Percentile(balances, 0.50),
            Percentile(balances, 0.75));
    }

    private static int Percentile(IReadOnlyList<int> sortedAscending, double percentile)
    {
        if (sortedAscending.Count == 1)
            return sortedAscending[0];

        var index = (int)Math.Round(percentile * (sortedAscending.Count - 1), MidpointRounding.AwayFromZero);
        index = Math.Clamp(index, 0, sortedAscending.Count - 1);
        return sortedAscending[index];
    }

    private static WealthClass ClassifyBalance(int balance, WealthThresholds thresholds)
    {
        if (balance <= thresholds.NewcomerMax)
            return WealthClass.Newcomers;
        if (balance <= thresholds.LowMax)
            return WealthClass.Low;
        if (balance <= thresholds.MidMax)
            return WealthClass.Mid;
        return WealthClass.High;
    }

    private string FormatAverageLifetime(IReadOnlyList<TimeSpan> lifetimes)
    {
        if (lifetimes.Count == 0)
            return Loc.GetString("adventure-webhook-shipstats-avg-na");

        var average = TimeSpan.FromSeconds(lifetimes.Average(span => span.TotalSeconds));
        if (average.TotalDays >= 1)
            return average.ToString(@"d\.hh\:mm", CultureInfo.InvariantCulture);

        return average.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    private enum WealthClass : byte
    {
        Newcomers,
        Low,
        Mid,
        High,
    }

    private readonly record struct WealthThresholds(int NewcomerMax, int LowMax, int MidMax);

    private sealed class RecordSummary
    {
        public int Count;
        public int AbandonedCount;
        public List<TimeSpan> Lifetimes = new();
    }
}
